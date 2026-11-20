using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading;
using CombatExtended;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace AbsolutelyMoreCannons
{
    /// <summary>
    /// Lightweight local HTTP/JSON bridge on 127.0.0.1:8765 that allows automated dev/AI agents
    /// to inspect logs, start a quicktest map, spawn buildings/materials/pawns, control CompAmmoCapacitor,
    /// step ticks, inspect pawn jobs, and capture UI screenshots.
    /// Uses TcpListener on 127.0.0.1:8765 to avoid Windows HttpListener URLACL admin requirements.
    /// </summary>
    [StaticConstructorOnStartup]
    public static class DevAgentBridge
    {
        public const int Port = 8765;
        private static TcpListener listener;
        private static Thread listenerThread;
        private static volatile bool running;

        private class MainThreadWorkItem
        {
            public Func<string> Action;
            public bool RunOnGUI;
            public string ResultJson;
            public Exception Error;
            public readonly ManualResetEventSlim Done = new ManualResetEventSlim(false);
        }

        private static readonly ConcurrentQueue<MainThreadWorkItem> updateQueue = new ConcurrentQueue<MainThreadWorkItem>();
        private static readonly ConcurrentQueue<MainThreadWorkItem> guiQueue = new ConcurrentQueue<MainThreadWorkItem>();

        static DevAgentBridge()
        {
            try
            {
                EnsureRunnerComponent();
                PatchRootUpdateAndGUI();
                StartServer();
            }
            catch (Exception ex)
            {
                Log.Error($"[AMC DevAgentBridge] Failed to initialize: {ex}");
            }
        }

        private static void EnsureRunnerComponent()
        {
            try
            {
                GameObject existing = GameObject.Find("AMC_DevAgentBridgeRunner");
                if (existing == null)
                {
                    GameObject go = new GameObject("AMC_DevAgentBridgeRunner");
                    UnityEngine.Object.DontDestroyOnLoad(go);
                    go.AddComponent<DevAgentBridgeRunner>();
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"[AMC DevAgentBridge] Could not attach MonoBehaviour runner: {ex.Message}");
            }
        }

        private static void PatchRootUpdateAndGUI()
        {
            try
            {
                var harmony = new Harmony("AbsolutelyMoreCannons.DevAgentBridge");
                var rootUpdate = AccessTools.Method(typeof(Root), nameof(Root.Update));
                if (rootUpdate != null)
                {
                    harmony.Patch(rootUpdate, postfix: new HarmonyMethod(typeof(DevAgentBridge), nameof(ProcessUpdateQueue)));
                }

                var rootOnGUI = AccessTools.Method(typeof(Root), nameof(Root.OnGUI));
                if (rootOnGUI != null)
                {
                    harmony.Patch(rootOnGUI, postfix: new HarmonyMethod(typeof(DevAgentBridge), nameof(ProcessGUIQueue)));
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"[AMC DevAgentBridge] Harmony root hook warning: {ex.Message}");
            }
        }

        private static void StartServer()
        {
            if (running) return;
            running = true;

            listenerThread = new Thread(ServerLoop)
            {
                Name = "AMC_DevAgentBridge_TcpListener",
                IsBackground = true
            };
            listenerThread.Start();
            Log.Message($"[AMC DevAgentBridge] Listening on http://127.0.0.1:{Port}/");
        }

        public static void ProcessUpdateQueue()
        {
            while (updateQueue.TryDequeue(out MainThreadWorkItem item))
            {
                try
                {
                    item.ResultJson = item.Action();
                }
                catch (Exception ex)
                {
                    item.Error = ex;
                }
                finally
                {
                    item.Done.Set();
                }
            }
        }

        public static void ProcessGUIQueue()
        {
            // Only process GUI queue during Repaint so screen capture gets the rendered IMGUI state
            if (Event.current != null && Event.current.type != EventType.Repaint)
                return;

            while (guiQueue.TryDequeue(out MainThreadWorkItem item))
            {
                try
                {
                    item.ResultJson = item.Action();
                }
                catch (Exception ex)
                {
                    item.Error = ex;
                }
                finally
                {
                    item.Done.Set();
                }
            }
        }

        private static string RunOnMainThread(Func<string> action, bool onGUI = false, int timeoutMs = 15000)
        {
            var item = new MainThreadWorkItem
            {
                Action = action,
                RunOnGUI = onGUI
            };

            if (onGUI)
                guiQueue.Enqueue(item);
            else
                updateQueue.Enqueue(item);

            if (!item.Done.Wait(timeoutMs))
            {
                return "{\"ok\":false,\"error\":\"Timed out waiting for RimWorld main thread\"}";
            }

            if (item.Error != null)
            {
                return $"{{\"ok\":false,\"error\":\"{EscapeJson(item.Error.Message)}\",\"stackTrace\":\"{EscapeJson(item.Error.StackTrace ?? "")}\"}}";
            }

            return item.ResultJson ?? "{\"ok\":true}";
        }

        private static void ServerLoop()
        {
            try
            {
                listener = new TcpListener(IPAddress.Loopback, Port);
                listener.Start();

                while (running)
                {
                    try
                    {
                        TcpClient client = listener.AcceptTcpClient();
                        ThreadPool.QueueUserWorkItem(_ => HandleClient(client));
                    }
                    catch (SocketException)
                    {
                        if (!running) break;
                    }
                    catch (Exception)
                    {
                        // Ignore transient accept errors
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[AMC DevAgentBridge] TcpListener failed on port {Port}: {ex.Message}");
            }
        }

        private static void HandleClient(TcpClient client)
        {
            using (client)
            {
                client.ReceiveTimeout = 5000;
                client.SendTimeout = 15000;
                using (NetworkStream stream = client.GetStream())
                {
                    try
                    {
                        byte[] buffer = new byte[16384];
                        int bytesRead = stream.Read(buffer, 0, buffer.Length);
                        if (bytesRead <= 0) return;

                        string rawRequest = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                        string[] lines = rawRequest.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
                        if (lines.Length == 0) return;

                        string[] requestParts = lines[0].Split(' ');
                        string method = requestParts.Length > 0 ? requestParts[0].ToUpperInvariant() : "GET";
                        string fullUrl = requestParts.Length > 1 ? requestParts[1] : "/";

                        string path = fullUrl;
                        string queryString = "";
                        int qIdx = fullUrl.IndexOf('?');
                        if (qIdx >= 0)
                        {
                            path = fullUrl.Substring(0, qIdx);
                            queryString = fullUrl.Substring(qIdx + 1);
                        }

                        Dictionary<string, string> query = ParseQueryString(queryString);
                        string responseJson = RouteRequest(method, path.ToLowerInvariant(), query);

                        byte[] bodyBytes = Encoding.UTF8.GetBytes(responseJson);
                        string header =
                            "HTTP/1.1 200 OK\r\n" +
                            "Content-Type: application/json; charset=utf-8\r\n" +
                            $"Content-Length: {bodyBytes.Length}\r\n" +
                            "Connection: close\r\n" +
                            "\r\n";
                        byte[] headerBytes = Encoding.UTF8.GetBytes(header);

                        stream.Write(headerBytes, 0, headerBytes.Length);
                        stream.Write(bodyBytes, 0, bodyBytes.Length);
                        stream.Flush();
                    }
                    catch (Exception ex)
                    {
                        try
                        {
                            string errJson = $"{{\"ok\":false,\"error\":\"{EscapeJson(ex.Message)}\"}}";
                            byte[] bodyBytes = Encoding.UTF8.GetBytes(errJson);
                            string header =
                                "HTTP/1.1 500 Internal Server Error\r\n" +
                                "Content-Type: application/json; charset=utf-8\r\n" +
                                $"Content-Length: {bodyBytes.Length}\r\n" +
                                "Connection: close\r\n\r\n";
                            byte[] headerBytes = Encoding.UTF8.GetBytes(header);
                            stream.Write(headerBytes, 0, headerBytes.Length);
                            stream.Write(bodyBytes, 0, bodyBytes.Length);
                        }
                        catch
                        {
                            // Ignore disconnect
                        }
                    }
                }
            }
        }

        private static Dictionary<string, string> ParseQueryString(string queryString)
        {
            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrEmpty(queryString)) return dict;

            string[] pairs = queryString.Split('&');
            foreach (string pair in pairs)
            {
                if (string.IsNullOrEmpty(pair)) continue;
                int eq = pair.IndexOf('=');
                if (eq >= 0)
                {
                    string k = Uri.UnescapeDataString(pair.Substring(0, eq).Replace('+', ' '));
                    string v = Uri.UnescapeDataString(pair.Substring(eq + 1).Replace('+', ' '));
                    dict[k] = v;
                }
                else
                {
                    dict[Uri.UnescapeDataString(pair)] = "true";
                }
            }
            return dict;
        }

        private static string RouteRequest(string method, string path, Dictionary<string, string> q)
        {
            switch (path)
            {
                case "/":
                case "/status":
                    return RunOnMainThread(() => HandleStatus());

                case "/logs":
                    return RunOnMainThread(() => HandleLogs(q));

                case "/logs/clear":
                    return RunOnMainThread(() =>
                    {
                        Log.Clear();
                        return "{\"ok\":true,\"message\":\"Logs cleared\"}";
                    });

                case "/quicktest":
                    return RunOnMainThread(() => HandleQuickTest(q));

                case "/load":
                    return RunOnMainThread(() => HandleQuickTest(q));

                case "/reflect":
                    return RunOnMainThread(() => HandleReflect(q));

                case "/godmode":
                    return RunOnMainThread(() => HandleGodMode(q));

                case "/spawn":
                    return RunOnMainThread(() => HandleSpawn(q));

                case "/select":
                    return RunOnMainThread(() => HandleSelect(q));

                case "/capacitor":
                    return RunOnMainThread(() => HandleCapacitorGet(q));

                case "/capacitor/recipes":
                    return RunOnMainThread(() => HandleCapacitorRecipes(q));

                case "/capacitor/target":
                    return RunOnMainThread(() => HandleCapacitorTarget(q));

                case "/capacitor/deposit":
                    return RunOnMainThread(() => HandleCapacitorDeposit(q));

                case "/capacitor/ticket":
                    return RunOnMainThread(() => HandleCapacitorTicket(q));

                case "/capacitor/substitute":
                    return RunOnMainThread(() => HandleCapacitorSubstitute(q));

                case "/capacitor/stepsize":
                    return RunOnMainThread(() => HandleCapacitorStepSize(q));

                case "/capacitor/upgrade":
                    return RunOnMainThread(() => HandleCapacitorUpgrade(q));

                case "/capacitor/module":
                    return RunOnMainThread(() => HandleCapacitorModule(q));

                case "/speed":
                    return RunOnMainThread(() => HandleSpeed(q));

                case "/tick":
                    return RunOnMainThread(() => HandleTick(q));

                case "/pawns":
                    return RunOnMainThread(() => HandlePawns(q));

                case "/pawns/forcehaul":
                    return RunOnMainThread(() => HandlePawnsForceHaul(q));

                case "/map/things":
                    return RunOnMainThread(() => HandleMapThings(q));

                case "/screenshot":
                    return RunOnMainThread(() => HandleScreenshot(q), onGUI: true);

                default:
                    return $"{{\"ok\":false,\"error\":\"Unknown endpoint: {EscapeJson(path)}\"}}";
            }
        }

        // ==================== ENDPOINT HANDLERS ====================

        private static string HandleStatus()
        {
            string progState = Current.ProgramState.ToString();
            Map map = Current.ProgramState == ProgramState.Playing ? Find.CurrentMap : null;
            int ticks = (Current.ProgramState == ProgramState.Playing && Find.TickManager != null)
                ? Find.TickManager.TicksGame
                : 0;
            string speed = (Current.ProgramState == ProgramState.Playing && Find.TickManager != null)
                ? Find.TickManager.CurTimeSpeed.ToString()
                : "None";

            int msgCount = 0, warnCount = 0, errCount = 0;
            foreach (LogMessage m in Log.Messages)
            {
                if (m.type == LogMessageType.Error) errCount++;
                else if (m.type == LogMessageType.Warning) warnCount++;
                else msgCount++;
            }

            List<string> selectedJson = new List<string>();
            if (Current.ProgramState == ProgramState.Playing && Find.Selector != null)
            {
                foreach (object obj in Find.Selector.SelectedObjects)
                {
                    if (obj is Thing t)
                    {
                        selectedJson.Add($"{{\"id\":\"{EscapeJson(t.ThingID)}\",\"def\":\"{EscapeJson(t.def.defName)}\",\"label\":\"{EscapeJson(t.LabelCap)}\",\"x\":{t.Position.x},\"z\":{t.Position.z}}}");
                    }
                }
            }

            string openTabName = "";
            if (Current.ProgramState == ProgramState.Playing && MainButtonDefOf.Inspect?.TabWindow is MainTabWindow_Inspect inspectWin)
            {
                openTabName = inspectWin.OpenTabType?.Name ?? "";
            }

            var sb = new StringBuilder();
            sb.Append("{");
            sb.Append("\"ok\":true,");
            sb.Append($"\"programState\":\"{EscapeJson(progState)}\",");
            sb.Append($"\"hasMap\":{(map != null ? "true" : "false")},");
            sb.Append($"\"mapSize\":\"{(map != null ? $"{map.Size.x}x{map.Size.z}" : "")}\",");
            sb.Append($"\"tick\":{ticks},");
            sb.Append($"\"timeSpeed\":\"{EscapeJson(speed)}\",");
            sb.Append($"\"godMode\":{(DebugSettings.godMode ? "true" : "false")},");
            sb.Append($"\"openInspectTab\":\"{EscapeJson(openTabName)}\",");
            sb.Append($"\"selected\":[{string.Join(",", selectedJson)}],");
            sb.Append($"\"logCounts\":{{\"messages\":{msgCount},\"warnings\":{warnCount},\"errors\":{errCount}}}");
            sb.Append("}");
            return sb.ToString();
        }

        private static string HandleLogs(Dictionary<string, string> q)
        {
            string typeFilter = q.TryGetValue("type", out string tf) ? tf : "";
            string search = q.TryGetValue("search", out string sf) ? sf : "";
            int limit = q.TryGetValue("limit", out string lf) && int.TryParse(lf, out int lVal) ? Mathf.Clamp(lVal, 1, 1000) : 100;
            bool includeStack = q.TryGetValue("stack", out string stf) && stf.Equals("true", StringComparison.OrdinalIgnoreCase);

            List<LogMessage> all = Log.Messages.ToList();
            if (!string.IsNullOrEmpty(typeFilter))
            {
                all = all.Where(m => m.type.ToString().Equals(typeFilter, StringComparison.OrdinalIgnoreCase)).ToList();
            }
            if (!string.IsNullOrEmpty(search))
            {
                all = all.Where(m => (m.text != null && m.text.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0) ||
                                     (m.StackTrace != null && m.StackTrace.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0)).ToList();
            }

            int start = Mathf.Max(0, all.Count - limit);
            List<string> items = new List<string>();
            for (int i = start; i < all.Count; i++)
            {
                LogMessage m = all[i];
                string stackPart = includeStack ? $",\"stackTrace\":\"{EscapeJson(m.StackTrace ?? "")}\"" : "";
                items.Add($"{{\"type\":\"{m.type}\",\"repeats\":{m.repeats},\"text\":\"{EscapeJson(m.text ?? "")}\"{stackPart}}}");
            }

            return $"{{\"ok\":true,\"totalMatching\":{all.Count},\"returned\":{items.Count},\"logs\":[{string.Join(",", items)}]}}";
        }

        private static string HandleQuickTest(Dictionary<string, string> q)
        {
            if (Current.ProgramState == ProgramState.Playing && Find.CurrentMap != null)
            {
                return "{\"ok\":true,\"message\":\"Already in Playing state with an active map.\"}";
            }

            string saveName = q != null && q.TryGetValue("save", out string s) && !string.IsNullOrWhiteSpace(s) ? s : "TEST";
            GameDataSaveLoader.LoadGame(saveName);
            return $"{{\"ok\":true,\"save\":\"{EscapeJson(saveName)}\",\"message\":\"Loading save '{EscapeJson(saveName)}' via GameDataSaveLoader.LoadGame.\"}}";
        }

        private static string HandleReflect(Dictionary<string, string> q)
        {
            if (!q.TryGetValue("type", out string typeName) || string.IsNullOrEmpty(typeName))
                return "{\"ok\":false,\"error\":\"Missing 'type'\"}";

            Type t = GenTypes.GetTypeInAnyAssembly(typeName) ?? Type.GetType(typeName);
            if (t == null)
                return $"{{\"ok\":false,\"error\":\"Type '{EscapeJson(typeName)}' not found\"}}";

            string filter = q.TryGetValue("member", out string mf) ? mf : "";
            var members = new List<string>();

            foreach (var m in t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            {
                if (!string.IsNullOrEmpty(filter) && m.Name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0) continue;
                string ps = string.Join(", ", m.GetParameters().Select(p => $"{p.ParameterType.Name} {p.Name}" + (p.HasDefaultValue ? $"={p.DefaultValue}" : "")));
                members.Add($"\"METHOD {(m.IsStatic ? "static " : "")}{m.ReturnType.Name} {m.Name}({EscapeJson(ps)})\"");
            }

            foreach (var f in t.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            {
                if (!string.IsNullOrEmpty(filter) && f.Name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0) continue;
                string val = "";
                if (f.IsStatic)
                {
                    try { val = $" = {f.GetValue(null)}"; } catch { }
                }
                members.Add($"\"FIELD {(f.IsStatic ? "static " : "")}{f.FieldType.Name} {f.Name}{EscapeJson(val)}\"");
            }

            return $"{{\"ok\":true,\"type\":\"{EscapeJson(t.FullName)}\",\"members\":[{string.Join(",", members)}]}}";
        }

        private static string HandleGodMode(Dictionary<string, string> q)
        {
            if (q.TryGetValue("enable", out string en))
            {
                DebugSettings.godMode = en.Equals("true", StringComparison.OrdinalIgnoreCase) || en == "1";
            }
            else
            {
                DebugSettings.godMode = !DebugSettings.godMode;
            }
            return $"{{\"ok\":true,\"godMode\":{(DebugSettings.godMode ? "true" : "false")}}}";
        }

        private static string HandleSpawn(Dictionary<string, string> q)
        {
            Map map = Find.CurrentMap;
            if (map == null) return "{\"ok\":false,\"error\":\"No active map\"}";

            if (!q.TryGetValue("def", out string defName) || string.IsNullOrWhiteSpace(defName))
            {
                return "{\"ok\":false,\"error\":\"Missing 'def' query parameter\"}";
            }

            IntVec3 pos = map.Center;
            if (q.TryGetValue("near", out string nearTarget) && nearTarget.Equals("capacitor", StringComparison.OrdinalIgnoreCase))
            {
                Thing fab = FindCapacitorBuilding(map, null);
                if (fab != null) pos = fab.Position + new IntVec3(3, 0, 0);
            }
            else if (Find.CameraDriver != null)
            {
                IntVec3 camPos = Find.CameraDriver.MapPosition;
                if (camPos.InBounds(map)) pos = camPos;
            }

            if (q.TryGetValue("x", out string xs) && int.TryParse(xs, out int xVal)) pos.x = xVal;
            if (q.TryGetValue("z", out string zs) && int.TryParse(zs, out int zVal)) pos.z = zVal;
            pos.x = Mathf.Clamp(pos.x, 1, map.Size.x - 2);
            pos.z = Mathf.Clamp(pos.z, 1, map.Size.z - 2);

            int count = q.TryGetValue("count", out string cs) && int.TryParse(cs, out int cVal) ? Mathf.Max(1, cVal) : 1;
            int rotInt = q.TryGetValue("rot", out string rs) && int.TryParse(rs, out int rVal) ? (rVal & 3) : 0;
            Rot4 rot = new Rot4(rotInt);

            string factionParam = q.TryGetValue("faction", out string fs) ? fs : "player";
            Faction faction = Faction.OfPlayer;
            if (factionParam.Equals("hostile", StringComparison.OrdinalIgnoreCase))
                faction = Find.FactionManager.FirstFactionOfDef(FactionDefOf.Pirate) ?? Find.FactionManager.RandomEnemyFaction();
            else if (factionParam.Equals("none", StringComparison.OrdinalIgnoreCase))
                faction = null;

            // Check if it's a ThingDef first
            ThingDef thingDef = DefDatabase<ThingDef>.GetNamedSilentFail(defName);
            Thing lastSpawned = null;
            if (thingDef != null)
            {
                ThingDef stuffDef = null;
                if (thingDef.MadeFromStuff)
                {
                    if (q.TryGetValue("stuff", out string stuffName))
                        stuffDef = DefDatabase<ThingDef>.GetNamedSilentFail(stuffName);
                    if (stuffDef == null)
                        stuffDef = GenStuff.DefaultStuffFor(thingDef);
                }

                // Clear cell if requested for buildings
                if (thingDef.category == ThingCategory.Building)
                {
                    CellRect rect = GenAdj.OccupiedRect(pos, rot, thingDef.size);
                    foreach (IntVec3 c in rect)
                    {
                        if (!c.InBounds(map)) continue;
                        List<Thing> existing = c.GetThingList(map).ToList();
                        foreach (Thing et in existing)
                        {
                            if (et.def.destroyable && (et.def.category == ThingCategory.Plant || et.def.category == ThingCategory.Filth || et.def.IsEdifice()))
                            {
                                et.Destroy(DestroyMode.Vanish);
                            }
                        }
                    }

                    Thing bld = ThingMaker.MakeThing(thingDef, stuffDef);
                    if (thingDef.CanHaveFaction && faction != null)
                        bld.SetFactionDirect(faction);

                    Thing spawnedBld = GenSpawn.Spawn(bld, pos, map, rot, WipeMode.VanishOrMoveAside);

                    // Auto-power option for powered buildings
                    bool autoPower = !q.TryGetValue("power", out string pStr) || pStr.Equals("true", StringComparison.OrdinalIgnoreCase);
                    if (autoPower && spawnedBld != null)
                    {
                        EnsureBuildingPowered(spawnedBld, map);
                    }

                    return $"{{\"ok\":true,\"id\":\"{EscapeJson(spawnedBld.ThingID)}\",\"def\":\"{EscapeJson(thingDef.defName)}\",\"x\":{spawnedBld.Position.x},\"z\":{spawnedBld.Position.z},\"count\":1}}";
                }
                else
                {
                    // Item / resource spawning (handle multi-stack if count > stackLimit)
                    int remaining = count;
                    int stackLimit = Mathf.Max(1, thingDef.stackLimit);
                    int stacksSpawned = 0;

                    while (remaining > 0)
                    {
                        int batch = Mathf.Min(remaining, stackLimit);
                        Thing item = ThingMaker.MakeThing(thingDef, stuffDef);
                        item.stackCount = batch;
                        CompAmmoCapacitor.TryPlaceThingSafe(item, pos, map, ThingPlaceMode.Near, out Thing placed);
                        lastSpawned = placed ?? item;
                        if (lastSpawned != null && lastSpawned.TryGetComp<CompForbiddable>() != null)
                        {
                            lastSpawned.SetForbidden(false, false);
                        }
                        remaining -= batch;
                        stacksSpawned++;
                    }

                    return $"{{\"ok\":true,\"id\":\"{EscapeJson(lastSpawned?.ThingID ?? "")}\",\"def\":\"{EscapeJson(thingDef.defName)}\",\"x\":{(lastSpawned?.Position.x ?? pos.x)},\"z\":{(lastSpawned?.Position.z ?? pos.z)},\"totalSpawned\":{count},\"stacks\":{stacksSpawned}}}";
                }
            }

            // Otherwise check PawnKindDef via isolated helper method so JIT doesn't bind PawnGenerator in HandleSpawn
            return TrySpawnPawnKind(defName, count, faction, pos, map);
        }

        private static string TrySpawnPawnKind(string defName, int count, Faction faction, IntVec3 pos, Map map)
        {
            PawnKindDef kindDef = DefDatabase<PawnKindDef>.GetNamedSilentFail(defName);
            if (kindDef != null)
            {
                Pawn lastPawn = null;
                var genMethod = typeof(PawnGenerator).GetMethods()
                    .FirstOrDefault(m => m.Name == "GeneratePawn" && m.GetParameters().Length >= 1 && m.GetParameters()[0].ParameterType == typeof(PawnKindDef));
                for (int i = 0; i < count; i++)
                {
                    Pawn p = null;
                    if (genMethod != null)
                    {
                        var parms = genMethod.GetParameters();
                        object[] args = new object[parms.Length];
                        args[0] = kindDef;
                        if (parms.Length > 1) args[1] = faction;
                        for (int j = 2; j < parms.Length; j++)
                        {
                            args[j] = parms[j].HasDefaultValue ? parms[j].DefaultValue : (parms[j].ParameterType.IsValueType ? Activator.CreateInstance(parms[j].ParameterType) : null);
                        }
                        p = (Pawn)genMethod.Invoke(null, args);
                    }
                    if (p != null)
                    {
                        GenSpawn.Spawn(p, pos, map);
                        lastPawn = p;
                    }
                }
                return $"{{\"ok\":true,\"id\":\"{EscapeJson(lastPawn?.ThingID ?? "")}\",\"pawnKind\":\"{EscapeJson(kindDef.defName)}\",\"name\":\"{EscapeJson(lastPawn?.LabelShort ?? "")}\",\"x\":{pos.x},\"z\":{pos.z},\"count\":{count}}}";
            }

            return $"{{\"ok\":false,\"error\":\"No ThingDef or PawnKindDef found named '{EscapeJson(defName)}'\"}}";
        }

        private static void EnsureBuildingPowered(Thing building, Map map)
        {
            var powerComp = building.TryGetComp<CompPowerTrader>();
            if (powerComp == null) return;

            powerComp.PowerOn = true;

            // If not connected to a power net with excess power, spawn a hidden/adjacent chemfuel generator + fuel
            if (powerComp.PowerNet == null || powerComp.PowerNet.CurrentEnergyGainRate() <= 0f)
            {
                ThingDef genDef = DefDatabase<ThingDef>.GetNamedSilentFail("ChemfuelPoweredGenerator")
                               ?? DefDatabase<ThingDef>.GetNamedSilentFail("WoodFiredGenerator");
                if (genDef != null)
                {
                    IntVec3 genPos = building.Position + new IntVec3(-(building.def.size.x / 2) - 2, 0, 0);
                    if (genPos.InBounds(map))
                    {
                        Thing gen = ThingMaker.MakeThing(genDef);
                        gen.SetFactionDirect(Faction.OfPlayer);
                        GenSpawn.Spawn(gen, genPos, map);
                        var refuel = gen.TryGetComp<CompRefuelable>();
                        if (refuel != null)
                        {
                            refuel.Refuel(refuel.Props.fuelCapacity);
                        }
                        map.powerNetManager.UpdatePowerNetsAndConnections_First();
                        powerComp.PowerOn = true;
                    }
                }
            }
        }

        private static string HandleSelect(Dictionary<string, string> q)
        {
            Map map = Find.CurrentMap;
            if (map == null) return "{\"ok\":false,\"error\":\"No active map\"}";

            Thing target = null;
            if (q.TryGetValue("id", out string idStr) && !string.IsNullOrEmpty(idStr))
            {
                target = map.listerThings.AllThings.FirstOrDefault(t => t.ThingID.Equals(idStr, StringComparison.OrdinalIgnoreCase));
            }
            else if (q.TryGetValue("def", out string defStr) && !string.IsNullOrEmpty(defStr))
            {
                target = map.listerThings.AllThings.FirstOrDefault(t => t.def.defName.Equals(defStr, StringComparison.OrdinalIgnoreCase));
            }
            else
            {
                target = FindCapacitorBuilding(map, null);
            }

            if (target == null)
            {
                return "{\"ok\":false,\"error\":\"Target thing not found on current map\"}";
            }

            Find.Selector.ClearSelection();
            Find.Selector.Select(target, playSound: false, forceDesignatorDeselect: true);

            if (!q.TryGetValue("jump", out string jumpStr) || !jumpStr.Equals("false", StringComparison.OrdinalIgnoreCase))
            {
                CameraJumper.TryJump(target);
            }

            bool openTab = !q.TryGetValue("opentab", out string otStr) || !otStr.Equals("false", StringComparison.OrdinalIgnoreCase);
            string openedTabName = "";
            if (openTab && Find.MainTabsRoot != null && MainButtonDefOf.Inspect != null)
            {
                Find.MainTabsRoot.SetCurrentTab(MainButtonDefOf.Inspect, playSound: false);
                if (MainButtonDefOf.Inspect.TabWindow is MainTabWindow_Inspect inspectWin)
                {
                    if (target.TryGetComp<CompAmmoCapacitor>() != null)
                    {
                        if (q.TryGetValue("tab", out string tabStr) && tabStr.IndexOf("upgrade", StringComparison.OrdinalIgnoreCase) >= 0)
                            inspectWin.OpenTabType = typeof(ITab_AmmoCapacitorUpgrades);
                        else
                            inspectWin.OpenTabType = typeof(ITab_AmmoCapacitor);
                    }
                    openedTabName = inspectWin.OpenTabType?.Name ?? "";
                }
            }

            return $"{{\"ok\":true,\"selectedId\":\"{EscapeJson(target.ThingID)}\",\"def\":\"{EscapeJson(target.def.defName)}\",\"x\":{target.Position.x},\"z\":{target.Position.z},\"openTab\":\"{EscapeJson(openedTabName)}\"}}";
        }

        private static Thing FindCapacitorBuilding(Map map, string idFilter)
        {
            if (map == null) return null;
            foreach (Thing t in map.listerThings.AllThings)
            {
                if (t.TryGetComp<CompAmmoCapacitor>() != null)
                {
                    if (string.IsNullOrEmpty(idFilter) || t.ThingID.Equals(idFilter, StringComparison.OrdinalIgnoreCase))
                        return t;
                }
            }
            return null;
        }

        private static string HandleCapacitorGet(Dictionary<string, string> q)
        {
            Map map = Find.CurrentMap;
            if (map == null) return "{\"ok\":false,\"error\":\"No active map\"}";

            q.TryGetValue("id", out string idFilter);
            Thing bld = FindCapacitorBuilding(map, idFilter);
            if (bld == null)
            {
                return "{\"ok\":false,\"error\":\"No building with CompAmmoCapacitor found on current map\"}";
            }

            CompAmmoCapacitor comp = bld.TryGetComp<CompAmmoCapacitor>();
            comp.EnsureMaterialDictionariesInitialized();

            // Optional power force flag
            if (q.TryGetValue("forcepower", out string fp) && fp.Equals("true", StringComparison.OrdinalIgnoreCase))
            {
                EnsureBuildingPowered(bld, map);
            }

            var matList = new List<string>();
            if (comp.Props?.materials != null)
            {
                foreach (var entry in comp.Props.materials)
                {
                    if (entry.thingDef == null) continue;
                    int stored = comp.GetStored(entry.thingDef);
                    int target = comp.GetTarget(entry.thingDef);
                    matList.Add(
                        $"{{\"def\":\"{EscapeJson(entry.thingDef.defName)}\"," +
                        $"\"label\":\"{EscapeJson(entry.thingDef.LabelCap)}\"," +
                        $"\"category\":\"{entry.category}\"," +
                        $"\"canSubstitute\":{(entry.canSubstitute ? "true" : "false")}," +
                        $"\"equivalenceValue\":{entry.equivalenceValue.ToString(CultureInfo.InvariantCulture)}," +
                        $"\"stored\":{stored}," +
                        $"\"target\":{target}}}");
                }
            }

            var ticketList = new List<string>();
            if (comp.tickets != null)
            {
                for (int i = 0; i < comp.tickets.Count; i++)
                {
                    AmmoCraftTicket t = comp.tickets[i];
                    ThingDef ammoDef = t.ProducedAmmoDef;
                    int ownedOnMap = (ammoDef != null) ? map.resourceCounter.GetCount(ammoDef) : 0;
                    float pct = (t.workProgress / t.TotalWorkAmount) * 100f;
                    bool shouldRun = comp.ShouldTicketRun(t);

                    ticketList.Add(
                        $"{{\"index\":{i}," +
                        $"\"recipe\":\"{EscapeJson(t.recipe?.defName ?? "")}\"," +
                        $"\"ammoDef\":\"{EscapeJson(ammoDef?.defName ?? "")}\"," +
                        $"\"label\":\"{EscapeJson(ammoDef?.LabelCap.ToString() ?? t.recipe?.LabelCap.ToString() ?? "")}\"," +
                        $"\"batchCount\":{t.ProducedCountPerBatch}," +
                        $"\"isContinuous\":{(t.isContinuous ? "true" : "false")}," +
                        $"\"countToCraft\":{t.countToCraft}," +
                        $"\"minThreshold\":{t.minThreshold}," +
                        $"\"maxThreshold\":{t.maxThreshold}," +
                        $"\"workProgress\":{t.workProgress.ToString("F1", CultureInfo.InvariantCulture)}," +
                        $"\"totalWork\":{t.TotalWorkAmount.ToString("F1", CultureInfo.InvariantCulture)}," +
                        $"\"progressPct\":{pct.ToString("F1", CultureInfo.InvariantCulture)}," +
                        $"\"materialsLocked\":{(t.materialsLocked ? "true" : "false")}," +
                        $"\"autoCraftingActive\":{(t.autoCraftingActive ? "true" : "false")}," +
                        $"\"paused\":{(t.paused ? "true" : "false")}," +
                        $"\"shouldRun\":{(shouldRun ? "true" : "false")}," +
                        $"\"ownedOnMap\":{ownedOnMap}}}");
                }
            }

            IntVec3 outCell = comp.GetOutputCell();
            var sb = new StringBuilder();
            sb.Append("{");
            sb.Append("\"ok\":true,");
            sb.Append($"\"id\":\"{EscapeJson(bld.ThingID)}\",");
            sb.Append($"\"def\":\"{EscapeJson(bld.def.defName)}\",");
            sb.Append($"\"x\":{bld.Position.x},\"z\":{bld.Position.z},");
            sb.Append($"\"outputCell\":{{\"x\":{outCell.x},\"z\":{outCell.z}}},");
            sb.Append($"\"isPoweredAndActive\":{(comp.IsPoweredAndActive ? "true" : "false")},");
            sb.Append($"\"allowSubstitution\":{(comp.allowSubstitution ? "true" : "false")},");
            sb.Append($"\"uiStepSize\":{comp.uiStepSize},");
            sb.Append($"\"totalAllocatedStorage\":{comp.TotalAllocatedStorage},");
            sb.Append($"\"totalStoredMaterials\":{comp.TotalStoredMaterials},");
            sb.Append($"\"maxTotalStorage\":{comp.MaxTotalStorage},");
            sb.Append($"\"upgradeLevel\":{comp.upgradeLevel},");
            sb.Append($"\"maxUpgradeLevel\":{comp.MaxUpgradeLevel},");
            sb.Append($"\"isUpgrading\":{(comp.isUpgrading ? "true" : "false")},");
            sb.Append($"\"upgradeMaterialsSatisfied\":{(comp.AreUpgradeMaterialsSatisfied ? "true" : "false")},");
            sb.Append($"\"upgradeWorkDone\":{comp.upgradeWorkDone.ToString("F1", CultureInfo.InvariantCulture)},");
            sb.Append($"\"upgradeWorkTotal\":{comp.UpgradeWorkTotal.ToString("F1", CultureInfo.InvariantCulture)},");
            sb.Append($"\"upgradeSpeedMultiplier\":{comp.CurrentUpgradeSpeedMultiplier.ToString(CultureInfo.InvariantCulture)},");
            sb.Append($"\"rebateChance\":{comp.CurrentRebateChance.ToString(CultureInfo.InvariantCulture)},");
            sb.Append($"\"rebatePercent\":{comp.CurrentRebatePercent.ToString(CultureInfo.InvariantCulture)},");
            sb.Append($"\"installedModule\":\"{EscapeJson(comp.InstalledModule?.def?.defName ?? "")}\",");
            sb.Append($"\"targetModule\":\"{EscapeJson(comp.targetModuleDef?.defName ?? "")}\",");
            sb.Append($"\"inspectString\":\"{EscapeJson(comp.CompInspectStringExtra())}\",");
            sb.Append($"\"availableRecipesCount\":{CompAmmoCapacitor.AllAmmoRecipes.Count},");
            sb.Append($"\"materials\":[{string.Join(",", matList)}],");
            sb.Append($"\"tickets\":[{string.Join(",", ticketList)}]");
            sb.Append("}");
            return sb.ToString();
        }

        private static string HandleCapacitorRecipes(Dictionary<string, string> q)
        {
            string search = q.TryGetValue("search", out string s) ? s.Trim() : "";
            int limit = q.TryGetValue("limit", out string ls) && int.TryParse(ls, out int lVal) ? Mathf.Clamp(lVal, 1, 500) : 50;

            var recipes = CompAmmoCapacitor.AllAmmoRecipes;
            if (!string.IsNullOrEmpty(search))
            {
                recipes = recipes.Where(r =>
                    (r.defName != null && r.defName.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0) ||
                    (r.label != null && r.label.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0) ||
                    (r.ProducedThingDef?.defName != null && r.ProducedThingDef.defName.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0) ||
                    (r.ProducedThingDef?.label != null && r.ProducedThingDef.label.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0)
                ).ToList();
            }

            var items = new List<string>();
            foreach (RecipeDef r in recipes.Take(limit))
            {
                var ingList = new List<string>();
                foreach (var ing in r.ingredients)
                {
                    ThingDef ingDef = ing.IsFixedIngredient ? ing.FixedIngredient : ing.filter?.AllowedThingDefs?.FirstOrDefault();
                    if (ingDef != null)
                    {
                        ingList.Add($"{{\"def\":\"{EscapeJson(ingDef.defName)}\",\"count\":{Mathf.CeilToInt(ing.GetBaseCount())}}}");
                    }
                }
                int batch = (r.products != null && r.products.Count > 0) ? r.products[0].count : 1;
                items.Add(
                    $"{{\"recipe\":\"{EscapeJson(r.defName)}\"," +
                    $"\"ammoDef\":\"{EscapeJson(r.ProducedThingDef?.defName ?? "")}\"," +
                    $"\"label\":\"{EscapeJson(r.ProducedThingDef?.LabelCap.ToString() ?? r.LabelCap.ToString())}\"," +
                    $"\"batchCount\":{batch}," +
                    $"\"workAmount\":{r.WorkAmountTotal(null).ToString("F0", CultureInfo.InvariantCulture)}," +
                    $"\"ingredients\":[{string.Join(",", ingList)}]}}");
            }

            return $"{{\"ok\":true,\"totalMatching\":{recipes.Count},\"returned\":{items.Count},\"recipes\":[{string.Join(",", items)}]}}";
        }

        private static string HandleCapacitorTarget(Dictionary<string, string> q)
        {
            Map map = Find.CurrentMap;
            if (map == null) return "{\"ok\":false,\"error\":\"No active map\"}";

            Thing bld = FindCapacitorBuilding(map, q.TryGetValue("id", out string id) ? id : null);
            if (bld == null) return "{\"ok\":false,\"error\":\"No CompAmmoCapacitor building on map\"}";

            if (!q.TryGetValue("def", out string defName) || string.IsNullOrEmpty(defName))
                return "{\"ok\":false,\"error\":\"Missing 'def' parameter\"}";

            ThingDef matDef = DefDatabase<ThingDef>.GetNamedSilentFail(defName);
            if (matDef == null)
                return $"{{\"ok\":false,\"error\":\"ThingDef '{EscapeJson(defName)}' not found\"}}";

            if (!q.TryGetValue("target", out string tStr) || !int.TryParse(tStr, out int targetVal))
                return "{\"ok\":false,\"error\":\"Missing or invalid 'target' integer parameter\"}";

            CompAmmoCapacitor comp = bld.TryGetComp<CompAmmoCapacitor>();
            comp.SetTarget(matDef, targetVal);

            return $"{{\"ok\":true,\"def\":\"{EscapeJson(matDef.defName)}\",\"stored\":{comp.GetStored(matDef)},\"target\":{comp.GetTarget(matDef)}}}";
        }

        private static string HandleCapacitorDeposit(Dictionary<string, string> q)
        {
            Map map = Find.CurrentMap;
            if (map == null) return "{\"ok\":false,\"error\":\"No active map\"}";

            Thing bld = FindCapacitorBuilding(map, q.TryGetValue("id", out string id) ? id : null);
            if (bld == null) return "{\"ok\":false,\"error\":\"No CompAmmoCapacitor building on map\"}";

            if (!q.TryGetValue("def", out string defName) || string.IsNullOrEmpty(defName))
                return "{\"ok\":false,\"error\":\"Missing 'def' parameter\"}";

            ThingDef matDef = DefDatabase<ThingDef>.GetNamedSilentFail(defName);
            if (matDef == null)
                return $"{{\"ok\":false,\"error\":\"ThingDef '{EscapeJson(defName)}' not found\"}}";

            int count = q.TryGetValue("count", out string cStr) && int.TryParse(cStr, out int cVal) ? Mathf.Max(1, cVal) : 75;
            CompAmmoCapacitor comp = bld.TryGetComp<CompAmmoCapacitor>();

            Thing tempStack = ThingMaker.MakeThing(matDef);
            tempStack.stackCount = count;
            int deposited = comp.DepositMaterial(tempStack, count);

            return $"{{\"ok\":true,\"def\":\"{EscapeJson(matDef.defName)}\",\"deposited\":{deposited},\"stored\":{comp.GetStored(matDef)},\"target\":{comp.GetTarget(matDef)}}}";
        }

        private static string HandleCapacitorTicket(Dictionary<string, string> q)
        {
            Map map = Find.CurrentMap;
            if (map == null) return "{\"ok\":false,\"error\":\"No active map\"}";

            Thing bld = FindCapacitorBuilding(map, q.TryGetValue("id", out string id) ? id : null);
            if (bld == null) return "{\"ok\":false,\"error\":\"No CompAmmoCapacitor building on map\"}";

            CompAmmoCapacitor comp = bld.TryGetComp<CompAmmoCapacitor>();

            if (q.TryGetValue("clear", out string clr) && clr.Equals("true", StringComparison.OrdinalIgnoreCase))
            {
                comp.tickets.Clear();
                if (!q.ContainsKey("recipe"))
                {
                    return "{\"ok\":true,\"message\":\"Cleared all tickets\",\"ticketCount\":0}";
                }
            }

            if (!q.TryGetValue("recipe", out string recipeName) || string.IsNullOrEmpty(recipeName))
                return "{\"ok\":false,\"error\":\"Missing 'recipe' parameter\"}";

            RecipeDef recipe = DefDatabase<RecipeDef>.GetNamedSilentFail(recipeName);
            if (recipe == null)
            {
                // Also allow matching by produced AmmoDef defName
                recipe = CompAmmoCapacitor.AllAmmoRecipes.FirstOrDefault(r =>
                    r.defName.Equals(recipeName, StringComparison.OrdinalIgnoreCase) ||
                    (r.ProducedThingDef != null && r.ProducedThingDef.defName.Equals(recipeName, StringComparison.OrdinalIgnoreCase)));
            }

            if (recipe == null)
                return $"{{\"ok\":false,\"error\":\"RecipeDef or AmmoDef '{EscapeJson(recipeName)}' not found\"}}";

            bool continuous = q.TryGetValue("continuous", out string contStr) &&
                              (contStr.Equals("true", StringComparison.OrdinalIgnoreCase) || contStr == "1");
            int count = q.TryGetValue("count", out string cntStr) && int.TryParse(cntStr, out int cVal) ? Mathf.Max(1, cVal) : 5;
            int min = q.TryGetValue("min", out string minStr) && int.TryParse(minStr, out int minVal) ? Mathf.Max(0, minVal) : 75;
            int max = q.TryGetValue("max", out string maxStr) && int.TryParse(maxStr, out int maxVal) ? Mathf.Max(min + 1, maxVal) : 750;
            bool paused = q.TryGetValue("paused", out string pStr) && pStr.Equals("true", StringComparison.OrdinalIgnoreCase);

            AmmoCraftTicket ticket = comp.tickets.FirstOrDefault(t => t.recipe == recipe);
            bool isNew = false;
            if (ticket == null)
            {
                ticket = new AmmoCraftTicket { recipe = recipe };
                comp.tickets.Add(ticket);
                isNew = true;
            }

            ticket.isContinuous = continuous;
            ticket.countToCraft = count;
            ticket.minThreshold = min;
            ticket.maxThreshold = max;
            ticket.paused = paused;

            // Sync ITab selection so the open UI immediately shows this ticket in the detail view
            try
            {
                if (MainButtonDefOf.Inspect?.TabWindow is MainTabWindow_Inspect inspectWin &&
                    inspectWin.OpenTabType == typeof(ITab_AmmoCapacitor))
                {
                    var tab = bld.GetInspectTabs()?.OfType<ITab_AmmoCapacitor>().FirstOrDefault();
                    if (tab != null)
                    {
                        var selMethod = AccessTools.Method(typeof(ITab_AmmoCapacitor), "SelectExistingTicket");
                        selMethod?.Invoke(tab, new object[] { ticket });
                    }
                }
            }
            catch
            {
                // Ignore UI reflection sync error
            }

            return $"{{\"ok\":true,\"isNew\":{(isNew ? "true" : "false")},\"recipe\":\"{EscapeJson(recipe.defName)}\",\"ammoDef\":\"{EscapeJson(recipe.ProducedThingDef?.defName ?? "")}\",\"isContinuous\":{(continuous ? "true" : "false")},\"countToCraft\":{count},\"minThreshold\":{min},\"maxThreshold\":{max},\"ticketCount\":{comp.tickets.Count}}}";
        }

        private static string HandleCapacitorSubstitute(Dictionary<string, string> q)
        {
            Map map = Find.CurrentMap;
            if (map == null) return "{\"ok\":false,\"error\":\"No active map\"}";

            Thing bld = FindCapacitorBuilding(map, q.TryGetValue("id", out string id) ? id : null);
            if (bld == null) return "{\"ok\":false,\"error\":\"No CompAmmoCapacitor building on map\"}";

            CompAmmoCapacitor comp = bld.TryGetComp<CompAmmoCapacitor>();
            if (q.TryGetValue("enable", out string en))
            {
                comp.allowSubstitution = en.Equals("true", StringComparison.OrdinalIgnoreCase) || en == "1";
            }
            else
            {
                comp.allowSubstitution = !comp.allowSubstitution;
            }

            return $"{{\"ok\":true,\"allowSubstitution\":{(comp.allowSubstitution ? "true" : "false")}}}";
        }

        private static string HandleCapacitorStepSize(Dictionary<string, string> q)
        {
            Map map = Find.CurrentMap;
            if (map == null) return "{\"ok\":false,\"error\":\"No active map\"}";

            Thing bld = FindCapacitorBuilding(map, q.TryGetValue("id", out string id) ? id : null);
            if (bld == null) return "{\"ok\":false,\"error\":\"No CompAmmoCapacitor building on map\"}";

            CompAmmoCapacitor comp = bld.TryGetComp<CompAmmoCapacitor>();
            if (q.TryGetValue("val", out string vs) && int.TryParse(vs, out int v))
            {
                comp.uiStepSize = Mathf.Clamp(v, 1, 10000);
            }
            return $"{{\"ok\":true,\"uiStepSize\":{comp.uiStepSize}}}";
        }

        private static string HandleCapacitorUpgrade(Dictionary<string, string> q)
        {
            Map map = Find.CurrentMap;
            if (map == null) return "{\"ok\":false,\"error\":\"No active map\"}";

            Thing bld = FindCapacitorBuilding(map, q.TryGetValue("id", out string id) ? id : null);
            if (bld == null) return "{\"ok\":false,\"error\":\"No CompAmmoCapacitor building on map\"}";

            CompAmmoCapacitor comp = bld.TryGetComp<CompAmmoCapacitor>();
            if (q.TryGetValue("cancel", out string cStr) && cStr.Equals("true", StringComparison.OrdinalIgnoreCase))
            {
                comp.CancelUpgrade();
                return "{\"ok\":true,\"isUpgrading\":false,\"message\":\"Upgrade cancelled\"}";
            }

            if (q.TryGetValue("instant", out string iStr) && iStr.Equals("true", StringComparison.OrdinalIgnoreCase))
            {
                comp.CompleteUpgrade();
                return $"{{\"ok\":true,\"upgradeLevel\":{comp.upgradeLevel},\"isUpgrading\":false}}";
            }

            bool ok = comp.StartUpgrade(out string msg);
            return $"{{\"ok\":{(ok ? "true" : "false")},\"upgradeLevel\":{comp.upgradeLevel},\"isUpgrading\":{(comp.isUpgrading ? "true" : "false")},\"speedMultiplier\":{comp.CurrentUpgradeSpeedMultiplier.ToString(CultureInfo.InvariantCulture)},\"rebatePercent\":{comp.CurrentRebatePercent.ToString(CultureInfo.InvariantCulture)},\"message\":\"{EscapeJson(msg ?? "")}\"}}";
        }

        private static string HandleCapacitorModule(Dictionary<string, string> q)
        {
            Map map = Find.CurrentMap;
            if (map == null) return "{\"ok\":false,\"error\":\"No active map\"}";

            Thing bld = FindCapacitorBuilding(map, q.TryGetValue("id", out string id) ? id : null);
            if (bld == null) return "{\"ok\":false,\"error\":\"No CompAmmoCapacitor building on map\"}";

            CompAmmoCapacitor comp = bld.TryGetComp<CompAmmoCapacitor>();

            if (q.TryGetValue("eject", out string ej) && ej.Equals("true", StringComparison.OrdinalIgnoreCase))
            {
                comp.targetModuleDef = null;
                comp.EjectInstalledModule();
                return "{\"ok\":true,\"message\":\"Ejected module\",\"installedModule\":\"\"}";
            }

            if (!q.TryGetValue("def", out string defName) || string.IsNullOrEmpty(defName))
                return "{\"ok\":false,\"error\":\"Missing 'def' parameter\"}";

            ThingDef modDef = DefDatabase<ThingDef>.GetNamedSilentFail(defName);
            if (modDef == null)
                return $"{{\"ok\":false,\"error\":\"Module ThingDef '{EscapeJson(defName)}' not found\"}}";

            bool direct = q.TryGetValue("direct", out string dStr) && dStr.Equals("true", StringComparison.OrdinalIgnoreCase);
            if (direct)
            {
                Thing item = ThingMaker.MakeThing(modDef);
                comp.InstallModuleDirect(item);
            }
            else
            {
                comp.targetModuleDef = modDef;
            }

            return $"{{\"ok\":true,\"installedModule\":\"{EscapeJson(comp.InstalledModule?.def?.defName ?? "")}\",\"targetModule\":\"{EscapeJson(comp.targetModuleDef?.defName ?? "")}\"}}";
        }

        private static string HandleSpeed(Dictionary<string, string> q)
        {
            if (Current.ProgramState != ProgramState.Playing || Find.TickManager == null)
                return "{\"ok\":false,\"error\":\"Not in Playing state\"}";

            if (q.TryGetValue("val", out string valStr))
            {
                if (int.TryParse(valStr, out int speedInt))
                {
                    Find.TickManager.CurTimeSpeed = (TimeSpeed)Mathf.Clamp(speedInt, 0, 4);
                }
                else if (Enum.TryParse<TimeSpeed>(valStr, true, out TimeSpeed parsed))
                {
                    Find.TickManager.CurTimeSpeed = parsed;
                }
            }

            return $"{{\"ok\":true,\"timeSpeed\":\"{Find.TickManager.CurTimeSpeed}\",\"tick\":{Find.TickManager.TicksGame}}}";
        }

        private static string HandleTick(Dictionary<string, string> q)
        {
            if (Current.ProgramState != ProgramState.Playing || Find.TickManager == null)
                return "{\"ok\":false,\"error\":\"Not in Playing state\"}";

            int count = q.TryGetValue("count", out string cs) && int.TryParse(cs, out int cVal) ? Mathf.Clamp(cVal, 1, 10000) : 60;
            int before = Find.TickManager.TicksGame;

            for (int i = 0; i < count; i++)
            {
                Find.TickManager.DoSingleTick();
            }

            return $"{{\"ok\":true,\"ticksStepped\":{count},\"tickBefore\":{before},\"tickAfter\":{Find.TickManager.TicksGame}}}";
        }

        private static string HandlePawns(Dictionary<string, string> q)
        {
            Map map = Find.CurrentMap;
            if (map == null) return "{\"ok\":false,\"error\":\"No active map\"}";

            bool all = q.TryGetValue("all", out string aStr) && aStr.Equals("true", StringComparison.OrdinalIgnoreCase);
            IEnumerable<Pawn> pawns = all ? map.mapPawns.AllPawnsSpawned : map.mapPawns.FreeColonistsSpawned;

            var list = new List<string>();
            foreach (Pawn p in pawns)
            {
                Job curJob = p.CurJob;
                string jobDef = curJob?.def?.defName ?? "None";
                string targetA = curJob != null && curJob.targetA.IsValid
                    ? (curJob.targetA.HasThing ? $"{curJob.targetA.Thing.def.defName}({curJob.targetA.Thing.ThingID})" : curJob.targetA.Cell.ToString())
                    : "";
                string targetB = curJob != null && curJob.targetB.IsValid
                    ? (curJob.targetB.HasThing ? $"{curJob.targetB.Thing.def.defName}({curJob.targetB.Thing.ThingID})" : curJob.targetB.Cell.ToString())
                    : "";
                int jobCount = curJob?.count ?? -1;

                Thing carried = p.carryTracker?.CarriedThing;
                string carriedStr = carried != null ? $"{carried.def.defName} x{carried.stackCount}" : "";

                // Ensure Hauling work type is enabled if requested
                if (q.TryGetValue("enablehaul", out string eh) && eh.Equals("true", StringComparison.OrdinalIgnoreCase) && p.workSettings != null)
                {
                    p.workSettings.EnableAndInitializeIfNotAlreadyInitialized();
                    p.workSettings.SetPriority(WorkTypeDefOf.Hauling, 1);
                }

                list.Add(
                    $"{{\"id\":\"{EscapeJson(p.ThingID)}\"," +
                    $"\"name\":\"{EscapeJson(p.LabelShort)}\"," +
                    $"\"x\":{p.Position.x},\"z\":{p.Position.z}," +
                    $"\"drafted\":{(p.Drafted ? "true" : "false")}," +
                    $"\"jobDef\":\"{EscapeJson(jobDef)}\"," +
                    $"\"targetA\":\"{EscapeJson(targetA)}\"," +
                    $"\"targetB\":\"{EscapeJson(targetB)}\"," +
                    $"\"jobCount\":{jobCount}," +
                    $"\"carried\":\"{EscapeJson(carriedStr)}\"}}");
            }

            return $"{{\"ok\":true,\"count\":{list.Count},\"pawns\":[{string.Join(",", list)}]}}";
        }

        private static string HandlePawnsForceHaul(Dictionary<string, string> q)
        {
            Map map = Find.CurrentMap;
            if (map == null) return "{\"ok\":false,\"error\":\"No active map\"}";

            Thing fab = FindCapacitorBuilding(map, q.TryGetValue("id", out string id) ? id : null);
            if (fab == null) return "{\"ok\":false,\"error\":\"No CompAmmoCapacitor building on map\"}";

            Pawn pawn = map.mapPawns.FreeColonistsSpawned.FirstOrDefault(p => !p.Dead && !p.Downed);
            if (pawn == null) return "{\"ok\":false,\"error\":\"No free colonist spawned on map\"}";

            if (pawn.Drafted) pawn.drafter.Drafted = false;
            if (pawn.workSettings != null)
            {
                pawn.workSettings.EnableAndInitializeIfNotAlreadyInitialized();
                pawn.workSettings.SetPriority(WorkTypeDefOf.Hauling, 1);
            }

            var worker = new WorkGiver_HaulToAmmoCapacitor();
            JobFailReason.Clear();
            bool hasJob = worker.HasJobOnThing(pawn, fab, forced: true);
            string failReason = JobFailReason.Reason ?? "";

            Job job = hasJob ? worker.JobOnThing(pawn, fab, forced: true) : null;
            bool started = false;
            if (job != null && (!q.TryGetValue("start", out string st) || !st.Equals("false", StringComparison.OrdinalIgnoreCase)))
            {
                started = pawn.jobs.TryTakeOrderedJob(job, JobTag.Misc);
            }

            return $"{{\"ok\":true,\"pawn\":\"{EscapeJson(pawn.LabelShort)}\",\"hasJob\":{(hasJob ? "true" : "false")},\"jobDef\":\"{EscapeJson(job?.def?.defName ?? "")}\",\"targetA\":\"{EscapeJson(job?.targetA.Thing?.LabelCap ?? "")}\",\"count\":{(job?.count ?? 0)},\"started\":{(started ? "true" : "false")},\"failReason\":\"{EscapeJson(failReason)}\"}}";
        }

        private static string HandleMapThings(Dictionary<string, string> q)
        {
            Map map = Find.CurrentMap;
            if (map == null) return "{\"ok\":false,\"error\":\"No active map\"}";

            string defFilter = q.TryGetValue("def", out string df) ? df : "";
            IEnumerable<Thing> things = map.listerThings.AllThings;

            if (!string.IsNullOrEmpty(defFilter))
            {
                things = things.Where(t => t.def.defName.IndexOf(defFilter, StringComparison.OrdinalIgnoreCase) >= 0);
            }

            int resourceCounterVal = 0;
            ThingDef exactDef = !string.IsNullOrEmpty(defFilter) ? DefDatabase<ThingDef>.GetNamedSilentFail(defFilter) : null;
            if (exactDef != null)
            {
                resourceCounterVal = map.resourceCounter.GetCount(exactDef);
            }

            var items = new List<string>();
            int totalStacks = 0;
            int totalCount = 0;
            foreach (Thing t in things)
            {
                totalStacks++;
                totalCount += t.stackCount;
                if (items.Count < 100)
                {
                    items.Add($"{{\"id\":\"{EscapeJson(t.ThingID)}\",\"def\":\"{EscapeJson(t.def.defName)}\",\"label\":\"{EscapeJson(t.LabelCap)}\",\"stackCount\":{t.stackCount},\"x\":{t.Position.x},\"z\":{t.Position.z}}}");
                }
            }

            return $"{{\"ok\":true,\"matchingStacks\":{totalStacks},\"matchingTotalCount\":{totalCount},\"resourceCounter\":{resourceCounterVal},\"things\":[{string.Join(",", items)}]}}";
        }

        private static string HandleScreenshot(Dictionary<string, string> q)
        {
            string outPath = q.TryGetValue("path", out string p) && !string.IsNullOrWhiteSpace(p)
                ? p
                : Path.Combine(Path.GetTempPath(), $"rimworld_amc_{DateTime.Now:yyyyMMdd_HHmmss}.png");

            string dir = Path.GetDirectoryName(outPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            int w = Screen.width;
            int h = Screen.height;
            Texture2D tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            try
            {
                tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
                tex.Apply();
                byte[] pngBytes = ImageConversion.EncodeToPNG(tex);
                File.WriteAllBytes(outPath, pngBytes);
            }
            finally
            {
                UnityEngine.Object.Destroy(tex);
            }

            return $"{{\"ok\":true,\"method\":\"ReadPixels\",\"path\":\"{EscapeJson(outPath)}\",\"width\":{w},\"height\":{h}}}";
        }

        private static string EscapeJson(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            var sb = new StringBuilder(s.Length + 16);
            foreach (char c in s)
            {
                switch (c)
                {
                    case '\\': sb.Append("\\\\"); break;
                    case '"': sb.Append("\\\""); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < ' ')
                            sb.AppendFormat("\\u{0:X4}", (int)c);
                        else
                            sb.Append(c);
                        break;
                }
            }
            return sb.ToString();
        }
    }

    public class DevAgentBridgeRunner : MonoBehaviour
    {
        public void Update()
        {
            DevAgentBridge.ProcessUpdateQueue();
        }

        public void OnGUI()
        {
            DevAgentBridge.ProcessGUIQueue();
        }
    }
}

# Custom Turret Image Thumbnails

Drop custom thumbnail image files (PNG or WebP format) into this directory.

### How to use custom thumbnails:
1. Place your image file here (e.g. `155mmGCT.png` or `M61Vulcan.png`).
2. Open `site/data/image_overrides.json`.
3. Add or update the mapping for the turret's `defName`:

```json
{
  "overrides": {
    "Turret_155mmGCT_Base": "images/turrets/155mmGCT.png",
    "Turret_20mmM61Vulcan_Base": "images/turrets/M61Vulcan.png"
  }
}
```

4. Re-run `powershell ./site/scripts/rebuild.ps1` (or `python site/scripts/extract_turrets.py`).
5. Refresh your browser page to view your custom thumbnail!

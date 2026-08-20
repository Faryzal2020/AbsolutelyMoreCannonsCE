import React from 'react';
import { Button, Icon, Modal } from './ui.jsx';

const AMMO_CLASSES = [
  'GrenadeHE', 'ExplosiveAP', 'GrenadeHETF', 'AMC_Guided',
  'ArmorPiercing', 'Sabot', 'IncendiaryAP', 'RocketHEAT', 'Antigrain', 'Custom',
];

const DAMAGE_DEFS = [
  'Bomb', 'Bullet', 'ArmorPiercing', 'Flame', 'Fragment', 'Stun', 'Custom',
];

const FUSE_TYPES = ['Flak', 'Altitude', 'Proximity', 'Timed'];

export default function AmmoEditorModal({ ammo, onApply, onClose }) {
  const [draft, setDraft] = React.useState(() => JSON.parse(JSON.stringify(ammo)));
  const [tab, setTab] = React.useState('item');
  const [saving, setSaving] = React.useState(false);

  const update = (pathStr, val) => {
    setDraft((prev) => {
      const next = JSON.parse(JSON.stringify(prev));
      const parts = pathStr.split('.');
      let cur = next;
      for (let i = 0; i < parts.length - 1; i++) {
        cur[parts[i]] = cur[parts[i]] || {};
        cur = cur[parts[i]];
      }
      cur[parts[parts.length - 1]] = val;
      return next;
    });
  };

  const handleSave = async (andClose = true) => {
    setSaving(true);
    try {
      const res = await onApply(draft.defName, draft);
      if (res && andClose) onClose();
    } finally {
      setSaving(false);
    }
  };

  const addIngredient = () => {
    setDraft((prev) => {
      const next = JSON.parse(JSON.stringify(prev));
      next.recipe = next.recipe || {};
      next.recipe.ingredients = next.recipe.ingredients || [];
      next.recipe.ingredients.push({ thingDef: 'Steel', count: 10 });
      return next;
    });
  };

  const removeIngredient = (idx) => {
    setDraft((prev) => {
      const next = JSON.parse(JSON.stringify(prev));
      next.recipe.ingredients.splice(idx, 1);
      return next;
    });
  };

  const updateIngredient = (idx, key, val) => {
    setDraft((prev) => {
      const next = JSON.parse(JSON.stringify(prev));
      next.recipe.ingredients[idx][key] = key === 'count' ? parseInt(val, 10) || 0 : val;
      return next;
    });
  };

  return (
    <Modal
      title={`Edit Ammunition: ${draft.label || draft.defName}`}
      width="900px"
      onClose={onClose}
      footer={
        <>
          <Button variant="primary" disabled={saving} onClick={() => handleSave(true)}>
            {saving ? 'Saving...' : 'Apply & Close'}
          </Button>
          <Button variant="secondary" disabled={saving} onClick={() => handleSave(false)}>
            Apply
          </Button>
          <Button variant="ghost" onClick={onClose}>Cancel</Button>
        </>
      }
    >
      <div className="ammo-editor-body">
        {/* Mode Availability Toggles */}
        <div className="mode-toggle-banner">
          <div className="mode-toggle-label">Fire Modes Enabled:</div>
          <label className="toggle-chk">
            <input
              type="checkbox"
              checked={draft.hasDirectMode}
              onChange={(e) => update('hasDirectMode', e.target.checked)}
            />
            <span>Direct Fire Mode</span>
          </label>
          <label className="toggle-chk">
            <input
              type="checkbox"
              checked={draft.hasIndirectMode}
              onChange={(e) => update('hasIndirectMode', e.target.checked)}
            />
            <span>Indirect Fire Mode</span>
          </label>
        </div>

        {/* Tab Bar */}
        <div className="tab-bar">
          <button className={tab === 'item' ? 'active' : ''} onClick={() => setTab('item')}>1. Item & Recipe</button>
          <button className={tab === 'payload' ? 'active' : ''} onClick={() => setTab('payload')}>2. Shared Payload</button>
          <button
            className={`${tab === 'direct' ? 'active' : ''} ${!draft.hasDirectMode ? 'disabled' : ''}`}
            disabled={!draft.hasDirectMode}
            onClick={() => setTab('direct')}
          >
            3. Direct Mode
          </button>
          <button
            className={`${tab === 'indirect' ? 'active' : ''} ${!draft.hasIndirectMode ? 'disabled' : ''}`}
            disabled={!draft.hasIndirectMode}
            onClick={() => setTab('indirect')}
          >
            4. Indirect Mode
          </button>
        </div>

        {/* Tab 1: Item & Recipe */}
        {tab === 'item' && (
          <div className="tab-pane">
            <div className="form-grid">
              <div className="field-group">
                <label>Label</label>
                <input type="text" value={draft.label || ''} onChange={(e) => update('label', e.target.value)} />
              </div>
              <div className="field-group">
                <label>Ammo Class</label>
                <select value={draft.ammoClass || ''} onChange={(e) => update('ammoClass', e.target.value)}>
                  {AMMO_CLASSES.map((c) => <option key={c} value={c}>{c}</option>)}
                </select>
              </div>
              <div className="field-group">
                <label>Market Value ($)</label>
                <input type="number" step="0.01" value={draft.stats?.marketValue ?? ''} onChange={(e) => update('stats.marketValue', e.target.value)} />
              </div>
              <div className="field-group">
                <label>Mass (kg)</label>
                <input type="number" step="0.01" value={draft.stats?.mass ?? ''} onChange={(e) => update('stats.mass', e.target.value)} />
              </div>
              <div className="field-group">
                <label>Bulk</label>
                <input type="number" step="0.01" value={draft.stats?.bulk ?? ''} onChange={(e) => update('stats.bulk', e.target.value)} />
              </div>
              <div className="field-group">
                <label>Texture Path</label>
                <input type="text" value={draft.textures?.ammo || ''} onChange={(e) => update('textures.ammo', e.target.value)} />
              </div>
            </div>

            <hr className="section-divider" />
            <h3>Crafting Recipe ({draft.recipe?.defName || 'No Recipe Def'})</h3>
            <div className="form-grid">
              <div className="field-group">
                <label>Work Amount</label>
                <input type="number" value={draft.recipe?.workAmount ?? ''} onChange={(e) => update('recipe.workAmount', parseInt(e.target.value, 10) || null)} />
              </div>
              <div className="field-group">
                <label>Product Yield Count</label>
                <input type="number" value={draft.recipe?.yieldCount ?? 1} onChange={(e) => update('recipe.yieldCount', parseInt(e.target.value, 10) || 1)} />
              </div>
            </div>

            <h4>Ingredients List</h4>
            <table className="sub-table">
              <thead>
                <tr>
                  <th>Ingredient ThingDef</th>
                  <th>Count</th>
                  <th>Actions</th>
                </tr>
              </thead>
              <tbody>
                {(draft.recipe?.ingredients || []).map((ing, idx) => (
                  <tr key={idx}>
                    <td>
                      <input type="text" value={ing.thingDef} onChange={(e) => updateIngredient(idx, 'thingDef', e.target.value)} />
                    </td>
                    <td>
                      <input type="number" value={ing.count} onChange={(e) => updateIngredient(idx, 'count', e.target.value)} />
                    </td>
                    <td>
                      <Button small variant="danger" onClick={() => removeIngredient(idx)}>Remove</Button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
            <Button small icon="plus" style={{ marginTop: '8px' }} onClick={addIngredient}>Add Ingredient</Button>
          </div>
        )}

        {/* Tab 2: Shared Payload */}
        {tab === 'payload' && (
          <div className="tab-pane">
            <div className="info-banner">
              💡 Changes to primary damage, fragments, airburst, and guidance will apply to <strong>both Direct and Indirect</strong> projectile variants simultaneously!
            </div>

            <h3>Primary Direct Hit & Explosion</h3>
            <div className="form-grid">
              <div className="field-group">
                <label>Damage Type</label>
                <select value={draft.payload?.damageDef || 'Bomb'} onChange={(e) => update('payload.damageDef', e.target.value)}>
                  {DAMAGE_DEFS.map((d) => <option key={d} value={d}>{d}</option>)}
                </select>
              </div>
              <div className="field-group">
                <label>Base Damage Amount</label>
                <input type="number" value={draft.payload?.damageAmountBase ?? ''} onChange={(e) => update('payload.damageAmountBase', parseInt(e.target.value, 10) || null)} />
              </div>
              <div className="field-group">
                <label>Sharp Pen (mm RHA)</label>
                <input type="number" step="0.1" value={draft.payload?.armorPenetrationSharp ?? ''} onChange={(e) => update('payload.armorPenetrationSharp', parseFloat(e.target.value) || null)} />
              </div>
              <div className="field-group">
                <label>Blunt Pen (MPa)</label>
                <input type="number" step="0.1" value={draft.payload?.armorPenetrationBlunt ?? ''} onChange={(e) => update('payload.armorPenetrationBlunt', parseFloat(e.target.value) || null)} />
              </div>
              <div className="field-group">
                <label>Explosion Radius (cells)</label>
                <input type="number" step="0.1" value={draft.payload?.explosionRadius ?? ''} onChange={(e) => update('payload.explosionRadius', parseFloat(e.target.value) || null)} />
              </div>
              <div className="field-group">
                <label>Sound Explode Def</label>
                <input type="text" value={draft.payload?.soundExplode || ''} onChange={(e) => update('payload.soundExplode', e.target.value)} />
              </div>
            </div>

            <hr className="section-divider" />
            <h3>Airburst Mechanism & Fuse</h3>
            <div className="form-grid">
              <div className="field-group">
                <label>Fuse Type</label>
                <select value={draft.airburst?.type || 'Flak'} onChange={(e) => update('airburst.type', e.target.value)}>
                  {FUSE_TYPES.map((f) => <option key={f} value={f}>{f}</option>)}
                </select>
              </div>
              <div className="field-group">
                <label>Arming Ticks</label>
                <input type="number" value={draft.airburst?.armingTicks ?? ''} onChange={(e) => update('airburst.armingTicks', parseInt(e.target.value, 10) || null)} />
              </div>
              <div className="field-group">
                <label>Proximity Radius (cells)</label>
                <input type="number" step="0.1" value={draft.airburst?.proximityRadius ?? ''} onChange={(e) => update('airburst.proximityRadius', parseFloat(e.target.value) || null)} />
              </div>
              <div className="field-group">
                <label>Burst Altitude (m)</label>
                <input type="number" step="0.1" value={draft.airburst?.burstAltitude ?? ''} onChange={(e) => update('airburst.burstAltitude', parseFloat(e.target.value) || null)} />
              </div>
            </div>

            <hr className="section-divider" />
            <h3>Guidance & Terminal Homing</h3>
            <div className="form-grid">
              <div className="field-group">
                <label>Homing Acceleration</label>
                <input type="number" step="0.01" value={draft.guidance?.homingAcceleration ?? ''} onChange={(e) => update('guidance.homingAcceleration', parseFloat(e.target.value) || null)} />
              </div>
              <div className="field-group">
                <label>Retarget Radius (cells)</label>
                <input type="number" step="0.1" value={draft.guidance?.retargetRadius ?? ''} onChange={(e) => update('guidance.retargetRadius', parseFloat(e.target.value) || null)} />
              </div>
              <div className="field-group">
                <label>Guidance Delay (ticks)</label>
                <input type="number" value={draft.guidance?.guidanceDelay ?? ''} onChange={(e) => update('guidance.guidanceDelay', parseInt(e.target.value, 10) || null)} />
              </div>
              <div className="field-group">
                <label>Trajectory Worker</label>
                <input type="text" value={draft.guidance?.trajectoryWorker || ''} onChange={(e) => update('guidance.trajectoryWorker', e.target.value)} />
              </div>
            </div>
          </div>
        )}

        {/* Tab 3: Direct Mode */}
        {tab === 'direct' && draft.hasDirectMode && (
          <div className="tab-pane">
            <h3>Direct Shell Ballistics ({draft.directBulletDef})</h3>
            <div className="form-grid">
              <div className="field-group">
                <label>Direct Speed (cells/s)</label>
                <input type="number" value={draft.direct?.speed ?? ''} onChange={(e) => update('direct.speed', parseInt(e.target.value, 10) || null)} />
              </div>
              <div className="field-group">
                <label>Pre-Impact Sound</label>
                <input type="text" value={draft.direct?.soundImpactAnticipate || ''} onChange={(e) => update('direct.soundImpactAnticipate', e.target.value)} />
              </div>
              <div className="field-group">
                <label className="toggle-chk" style={{ marginTop: '24px' }}>
                  <input type="checkbox" checked={Boolean(draft.direct?.dropsCasings)} onChange={(e) => update('direct.dropsCasings', e.target.checked)} />
                  <span>Drops Casings</span>
                </label>
              </div>
            </div>
          </div>
        )}

        {/* Tab 4: Indirect Mode */}
        {tab === 'indirect' && draft.hasIndirectMode && (
          <div className="tab-pane">
            <h3>Indirect Shell & Global Map Shelling ({draft.indirectBulletDef})</h3>
            <div className="form-grid">
              <div className="field-group">
                <label>Global Map Tiles/Tick</label>
                <input type="number" step="0.01" value={draft.indirect?.shellingTilesPerTick ?? ''} onChange={(e) => update('indirect.shellingTilesPerTick', parseFloat(e.target.value) || null)} />
              </div>
              <div className="field-group">
                <label>Shelling Range (tiles)</label>
                <input type="number" value={draft.indirect?.shellingRange ?? ''} onChange={(e) => update('indirect.shellingRange', parseInt(e.target.value, 10) || null)} />
              </div>
              <div className="field-group">
                <label>Shelling Map Damage Multiplier</label>
                <input type="number" step="0.01" value={draft.indirect?.shellingDamage ?? ''} onChange={(e) => update('indirect.shellingDamage', parseFloat(e.target.value) || null)} />
              </div>
              <div className="field-group">
                <label>Roof Impact Sound</label>
                <input type="text" value={draft.indirect?.soundHitThickRoof || ''} onChange={(e) => update('indirect.soundHitThickRoof', e.target.value)} />
              </div>
            </div>
          </div>
        )}
      </div>
    </Modal>
  );
}

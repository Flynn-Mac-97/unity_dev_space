# Unity bridge — command reference

Direct TCP client to the live Unity editor (Codely Tuanjie bridge, `cn.tuanjie.codely.bridge`). Zero LLM cost. Driven by `unity_bridge.py` (protocol reversed 2026-06-23, see that file's docstring).

```python
from unity_bridge import UnityBridge
b = UnityBridge().connect()          # auto-reads port from .com-unity-codely.json
b.call("manage_scene", "get_hierarchy")
b.call("manage_gameobject", "create", name="Foo", parent="MANAGERS")
b.close()
```

Request envelope on the wire: `{"type":<command>,"params":{"action":<action>,...extra},"request_id":<id>}`.
Response: `{"success":bool,"message":str,"data":{...},"request_id":...}` — the real payload is usually nested at `resp["data"]["data"]`.

Requires only the **Unity editor running** (the bridge server is the editor itself). No Codely process needed.

## Commands + actions (enums extracted from codely.exe)

| Command | Actions |
|---------|---------|
| `manage_scene` | `get_hierarchy`, `get_active`, `get_build_settings`, `create`, `load`, `save`, `ensure_scene_open`, `ensure_scene_saved` |
| `manage_gameobject` | `find`, `list_children`, `get_components`, `select`, `create`, `modify`, `delete`, `add_component`, `remove_component`, `set_component_property`, `set_component_properties`, `create_batch`, `edit_batch`, `ensure_component`, `ensure_renderer_material`, `ensure_mesh_collider_mesh`, `ensure_prefab_default_sprite` |
| `manage_editor` | `get_state`, `get_current_state`, `get_project_root`, `get_selection`, `set_active_tool`, `get_active_tool`, `get_windows`, `focus_window`, `play`, `pause`, `stop`, `request_compile`, `start_compilation_pipeline`, `wait_for_compile`, `get_compilation_summary`, `wait_for_idle`, `ensure_tag`, `add_tag`, `remove_tag`, `get_tags`, `ensure_layer`, `add_layer`, `remove_layer`, `get_layers` |
| `manage_asset` | `search`, `get_info`, `get_components`, `import`, `import_asset`, `create`, `modify`, `delete`, `duplicate`, `move`, `rename`, `create_folder`, `create_batch`, `edit_batch`, `ensure_has_meta`, `ensure_meta_integrity` |
| `manage_script` | `read`, `create`, `update`, `edit`, `apply_text_edits`, `delete`, `validate`, `get_sha` |
| `manage_shader` | `detect_render_pipeline`, `ensure_material_shader_for_srp`, `create`, `read`, `update`, `delete` |
| `manage_screenshot` | `capture_game_view`, `capture_scene_view`, `capture_main_camera`, `capture_scene_camera`, `capture_specific_camera`, `capture_asset`, `capture_ui_toolkit`, `capture`. **Verified 2026-07-03:** bare call works — saves `screenshots/<View>_<timestamp>.png` 1920×1080; payload at `resp["data"]` DIRECTLY (`path`/`width`/`height`/`warning`), not `data.data`. Optional `path` param = output DIR. `filename` param breaks it (null data). Read the PNG file for vision. |
| `manage_ui` | `create_uxml`, `create_uss`, `link_uss_to_uxml`, `ensure_panel_settings_asset` |
| `manage_bake` | `bake_navmesh`, `bake_lighting`, `wait_for_bake`, `clear_navmesh`, `clear_baked_data` |
| `manage_package` | `install_package`, `remove_package`, `list_packages`, `wait_for_upm` |
| `manage_workflow` | `init_session`, `compile_and_validate`, `checkpoint`, `install_package_and_validate` (also `init`/`setup`/`build`/`validate`/`screenshot`) |
| `read_console` | actions are `get` / `clear` ONLY (verified 2026-07-03 — a level name as action → "Unknown action"). Filter via params: `b.call("read_console", "get", types=["error","warning"], count=10)`. |
| `execute_menu_item` | run an editor menu path (params: menu path) |
| `execute_csharp_script` | run arbitrary C# in the editor — escape hatch for anything above. **param key is `script` (NOT `code`); command name is `execute_csharp_script` (NOT `execute_csharp`).** A bare `return <expr>;` works; returns `data.data.result` (stringified) + `data.data.logs`. Use `b.csharp("<C#>")`. |
| `ping` | keepalive → `{"success":true,"message":"pong"}` (use `b.ping()`) |

> Action param shapes (extra keys per action) aren't fully documented — inspect a response or use `execute_csharp` reflection when unsure. Most actions ignore unknown actions silently (return generic success), so verify via the returned `data`, not just `success`.

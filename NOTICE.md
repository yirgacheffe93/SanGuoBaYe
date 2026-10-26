# NOTICE

本项目 `rewrite` 分支使用 Godot + C#，按照 `main` 分支的 `SGBY.lib` 内容与功能重写。

- `content/main/` 由 `main/libs/SGBY.lib` 通过可重复导入工具生成，保留原始索引、来源提交和内容哈希。
- main 的 bridge/WASM 仅用于离线导入与 golden 对照，不进入 Godot 生产运行时。
- `godot` 分支中的 iBaye 原生验证代码是独立研究原型，不参与构建。
- R0 的原创内容仅作为历史技术验证保留，不再代表当前内容方向。
- 商标、授权及其他合规事项按项目决定后置，后续发行检查另行处理。

具体来源见 `docs/provenance.md`，当前技术路线见 ADR-0009。

# 杀戮工厂（KillerFactory）

《杀戮尖塔2》自定义角色模组，基于 RitsuLib 与 Godot 4.5.1 Mono 开发。

## 开发环境

- Slay the Spire 2 `0.108.0`
- Godot .NET `4.5.1`
- .NET SDK 9 或更高版本
- RitsuLib（版本固定在 `KillerFactory.csproj`，运行时依赖会同步到 `KillerFactory.json`）

复制 `local.props.template` 为 `local.props`，填写游戏和 Godot 的本机路径。`local.props` 不会提交到 Git。

## 构建

仅验证 C# 编译，不复制到游戏、不导出 PCK：

```powershell
dotnet build .\KillerFactory.csproj /p:RunPckExport=false /p:CopyModOnBuild=false
```

完整构建并部署 DLL、manifest 和 PCK：

```powershell
dotnet build .\KillerFactory.csproj
```

完整产物位于游戏目录的 `mods/KillerFactory/`。

## 目录

- `KillerFactoryCode/`：C# 代码
- `KillerFactory/`：Godot 场景、图片和本地化
- `游戏设计思路.txt`：玩法设计草案
- `IMPLEMENTATION_STATUS.md`：当前垂直切片的完成项、设计差异和后续阶段
- `D:\godot_project\reference`：本机参考资料，不属于仓库

## 依赖

- [STS2-RitsuLib](https://github.com/BAKAOLC/STS2-RitsuLib)
- [模组制作教程](https://github.com/GlitchedReme/SlayTheSpire2ModdingTutorials)

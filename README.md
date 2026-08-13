# 总装师（Assembler）实验分支

《杀戮尖塔2》自定义角色实验模组，基于 RitsuLib 与 Godot 4.5.1 Mono 开发。

本分支只保留总装师角色：调整手牌顺序，将连续相邻的零件装配成具有费用、模块和耐久的临时产物。

## 开发环境

- Slay the Spire 2 `0.110.x`
- Godot .NET `4.5.1`
- .NET SDK 9 或更高版本
- RitsuLib `0.5.4`

复制 `local.props.template` 为 `local.props`，填写游戏与 Godot 的本机路径。

## 构建

仅验证 C#：

```powershell
dotnet build .\KillerFactory.csproj -p Platform=x64 -p:RunPckExport=false
```

完整构建、导出 PCK 并部署：

```powershell
dotnet build .\KillerFactory.csproj -p Platform=x64
```

## 保留内容

- `KillerFactoryCode/Cards/AssemblerCards.cs`：初始牌、产物和基础奖励牌
- `KillerFactoryCode/Cards/AssemblerRewardCards.cs`：后续奖励卡池
- `KillerFactoryCode/Mechanics/AssemblerMechanics.cs`：装配、模块、耐久和维修
- `review/当前卡牌逻辑审核_审核整理版.xlsx`：总装师卡牌审核表

旧杀戮工厂角色的加工台、机械产线、材料、融锻、旧卡池与设计文档已从本分支移除。

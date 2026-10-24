# Foresee

地图信息预览、路线规划与 Frozen Eye 辅助显示。

这是已发布版本 **0.1.3** 的源代码快照。[Steam 创意工坊](https://steamcommunity.com/sharedfiles/filedetails/?id=3754074086)。原作者署名保留在 mod 清单中。

## 构建

需要 .NET SDK（net10.0）、Godot 4.5.1 .NET SDK，以及你自己安装的《杀戮尖塔 2》。此项目不依赖 BaseLib。

```powershell
dotnet build foresee.csproj -c Release -p:Sts2Dir="<游戏目录>"
```

也可设置环境变量 `STS2_DIR`。`Sts2DataDir` 可以单独指定，以适配不同的游戏目录布局。

项目保留运行时程序集名称 `foresee`，请勿随仓库名称修改。构建不会自动安装或覆盖游戏里的 mod。输出位于 `.godot/mono/temp/bin/Release/`。发行时使用同名 DLL、JSON 和 PCK。

## 范围与兼容性

这里只发布代码，不包含游戏程序集、用户配置、游戏存档、私有音频或游戏素材。

游戏更新可能改变内部 API。具体构建与验证记录见 [VALIDATION.md](VALIDATION.md)；编译通过不等于所有游戏内交互均已重新测试。

原始代码采用 [MIT](LICENSE) 许可；第三方依赖见 [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md)。

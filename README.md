# MioKit.Expression

MioKit 的表达式计算插件。在搜索框中直接完成数学计算、表达式求值和自定义 JavaScript 函数调用；回车即可复制结果，并内置 Monaco 脚本工作台。

表达式由 [NCalc](https://github.com/sklose/NCalc) 解析，自定义函数由 ClearScript V8 执行。

## 功能

- **搜索框即输即算**：开启「全局搜索直接计算」后，输入 `1+2*3` 实时预览，回车复制；空框可计算剪贴板中的单行表达式
- **命令作用域**：`calc` / `expression` 进入计算作用域（无需开启全局计算）
- **剪贴板参数**：复制 `1,2,3` 后输入 `add`，等价于 `add(1,2,3)`
- **自定义 JS 函数**：在 `window.expression` 上挂载函数，即可在表达式中调用
- **脚本工作台**：WebView2 + Vue 3，Monaco 编辑器、编译检查、函数清单、运行日志与全局设置
- **结果格式化**：可配置小数精度（0–28）与千分位显示

## 配置说明

可在脚本工作台「全局设置」中调整；「全局搜索直接计算」也可在宿主「插件功能设置 → 配置 → 搜索」中切换，两边共用同一配置。

| 配置 | 默认 | 说明 |
|---|---|---|
| 全局搜索直接计算 | 关 | 开启后，全局搜索框可直接识别并计算表达式；关闭时仍可用 `calc` / `expression` 进入计算作用域 |
| 自定义脚本 | 开 | 开启后加载内置脚本与用户脚本（`window.expression`）；关闭后脚本引擎停止，自定义函数与内置 JS 函数均不生效，仅保留 NCalc 算术与内置数学函数 |
| 千分位分隔符 | 关 | 开启后将 `1000000` 显示为 `1,000,000` |
| 小数精度 | 10 | decimal 保留 0–28 位小数；`0` 表示舍入到整数 |

脚本、精度与千分位保存在插件数据目录的 `settings.json`。修改后需在工作台点击「应用更改」才会保存并生效；关闭「自定义脚本」时，编辑器中的脚本内容仍会保留，但不会加载执行。

## 用法示例

| 输入 | 结果 |
|---|---|
| `1 + 2 * 3` | `7` |
| `10% + 0.2` | `0.3` |
| `sqrt(144)` | `12` |
| `add(1, 2, 3)` | `6`（需开启「自定义脚本」并添加add方法） |

自定义函数示例（需开启「自定义脚本」并应用更改）：

```javascript
window.expression = Object.assign(window.expression || {}, {
  double: (n) => n * 2,
});
```

保存后在搜索框输入 `double(21)` 得到 `42`。内置函数包括 `add`、`multiply`、`avg`、`factorial`、`fib`、`isPrime`、`toBase` / `fromBase`、`roundTo`、`percent`、`random` / `randomInt` 等。

运算符支持算术、比较、逻辑与三元；`10%` 按百分比处理，`10 % 3` 仍为取模。更多语法见 [NCalc](https://github.com/sklose/NCalc)。

## 项目结构

```text
plugin/     # 插件源码、vue-ui、plugin.json
preview/    # Avalonia 预览宿主
tests/      # xUnit 测试
pack.ps1    # 打包脚本
```

前端构建产物输出到 `plugin/ui/dist`（不入库，打包时写入 nupkg）。

## 开发

```powershell
cd plugin\vue-ui
pnpm install
pnpm run build

cd ..\..
dotnet restore MioKit.Expression.slnx
dotnet test tests/MioKit.Expression.Tests.csproj -c Release
dotnet build MioKit.Expression.slnx -c Release
```

调试前端时可在 `plugin/vue-ui` 运行 `pnpm run dev`（默认 `http://localhost:5003`）。

打包：

```powershell
.\pack.ps1 -PackageVersion 1.0.1
```

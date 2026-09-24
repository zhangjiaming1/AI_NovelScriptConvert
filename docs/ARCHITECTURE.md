# TodoFlow 架构设计

## 1. 设计目标

TodoFlow 的目标不是做一个只显示列表的演示程序，而是交付一个结构完整、可继续扩展的离线桌面应用。设计时优先满足以下约束：

- 核心功能离线可用，不依赖第三方 API。
- UI 与业务状态解耦，避免把筛选、统计、保存逻辑写进窗口事件。
- 数据写入可靠，异常时不让程序崩溃。
- 当前规模不过度工程化，不为了“可能永远用不到”的未来提前引入复杂基础设施。
- 为后续增加分类、提醒、同步、数据库存储保留清晰扩展点。

## 2. 分层结构

| 层 | 目录 | 职责 | 不应该做什么 |
|---|---|---|---|
| View | `Views/` | XAML 布局、数据绑定、窗口生命周期 | 不直接读写 JSON，不实现业务筛选 |
| ViewModel | `ViewModels/` | 页面状态、命令、筛选、统计、编辑草稿、保存协调 | 不创建具体控件，不直接操作窗口 |
| Model | `Models/` | 可序列化的领域数据与枚举 | 不依赖 UI 框架 |
| Infrastructure | `Infrastructure/` | 数据读写、存储格式、原子写入 | 不承载界面状态和交互规则 |

依赖方向：

```text
Views -> ViewModels -> Models
                   -> ITodoRepository -> JsonTodoRepository
```

`ITodoRepository` 是 ViewModel 与具体存储之间的边界。当前实现使用本地 JSON；如果以后改用 SQLite，只需要替换仓储实现。

## 3. 主要对象

### `TodoItem`

领域模型，保存任务的最小完整状态：

- `Id`：稳定标识，方便未来做同步或数据库主键。
- `Title` / `Notes`：任务内容。
- `IsCompleted`：完成状态。
- `DueDate`：可选截止时间，使用 `DateTimeOffset?` 保留时区信息。
- `Priority`：低、普通、高。
- `CreatedAt`：排序和审计信息。

### `TodoItemViewModel`

面向列表项的状态适配器：

- 把 Model 中的字段转换成 UI 可直接绑定的文本。
- 计算 `IsOverdue`、`DueDateText`、`PriorityText`、`StatusText`。
- 通过回调通知主 ViewModel 任务状态发生变化。
- 提供单项删除命令。

这样做的原因是：UI 不需要值转换器堆叠，也不会把“今天截止”“已逾期”这类展示规则塞进 Model。

### `TodoItemEditorViewModel`

右侧详情面板的编辑草稿：

- 复制被选中任务的当前值。
- 用户修改时只改变草稿。
- 点击“保存修改”后才写回任务。
- 点击“取消”直接重新创建草稿。

该设计避免编辑到一半时直接污染列表数据，也避免为了短暂取消操作实现复杂的撤销栈。

### `MainWindowViewModel`

应用协调中心，负责：

- 加载和保存任务集合。
- 新建、删除、清除已完成任务。
- 维护全部任务和当前可见任务两套视图数据。
- 处理筛选和搜索。
- 维护选中任务及其编辑草稿。
- 计算总数、进行中数量、已完成数量和完成百分比。
- 维护状态提示与忙碌状态。

当前项目规模下，把这些协调逻辑保留在一个页面级 ViewModel 中比拆成多个事件总线或服务更直接，也更容易理解和测试。达到更大规模后，可以再拆出查询服务和命令处理器。

### `JsonTodoRepository`

负责 JSON 持久化：

- 默认路径为 `%LOCALAPPDATA%\TodoFlow\todos.json`。
- 使用 CamelCase 和字符串枚举，便于人工查看。
- 使用 `SemaphoreSlim` 串行化写入，避免并发保存互相覆盖。
- 先写 `todos.json.tmp`，再替换正式文件，降低进程中断导致文件半写入的风险。

## 4. 数据流

### 新建任务

```text
用户输入
  -> MainWindowViewModel.AddCommand
  -> 创建 TodoItem
  -> 包装为 TodoItemViewModel
  -> 刷新可见列表和统计
  -> JsonTodoRepository.SaveAsync
```

### 完成任务

```text
CheckBox
  -> TodoItemViewModel.IsCompleted
  -> 主 ViewModel 的变更回调
  -> 更新统计；若当前筛选会隐藏该项则刷新列表
  -> 自动保存
```

### 编辑任务

```text
选择列表项
  -> MainWindowViewModel.SelectedTodo
  -> 创建 TodoItemEditorViewModel
  -> 用户修改草稿
  -> SaveCommand 写回 TodoItemViewModel
  -> 刷新派生文本并自动保存
```

### 筛选与搜索

- `_allTodos` 保存完整集合。
- `VisibleTodos` 保存当前 UI 需要展示的集合。
- `MatchesCurrentView` 是唯一筛选规则入口。
- 筛选或搜索改变时重建可见集合，并保留仍可见的选中项。

这种“双集合”方案比在 View 中组合多个条件更明确，也比引入复杂 CollectionView 更适合当前功能规模。

## 5. 关键设计决策

### 为什么使用手工 MVVM，而不是引入额外的 MVVM 框架

项目只需少量命令和属性通知。`ViewModelBase` 与 `RelayCommand` 共约一百行代码，已经覆盖需求，同时减少包依赖和版本耦合。若任务继续增长，再引入 CommunityToolkit.Mvvm 的源生成器会更容易。

### 为什么使用 Repository 接口

ViewModel 不应该知道数据是从 JSON、SQLite 还是云端来的。Repository 边界让存储实现可替换，也让未来的单元测试可以使用内存仓储。

### 为什么让 View 保持薄

窗口中只保留 `InitializeAsync` 生命周期调用。新增、筛选、编辑和保存都在 ViewModel 中完成，便于独立测试和无界面验证。

### 为什么使用编辑草稿

直接绑定原对象会让“取消”很难实现。`TodoItemEditorViewModel` 用少量内存换取清晰的行为边界：保存前，用户操作只影响草稿。

### 为什么当前不引入依赖注入容器

应用只有一个窗口、一个主 ViewModel 和一个仓储。`App.OnFrameworkInitializationCompleted` 中显式组装依赖更透明，也更容易追踪对象生命周期。服务数量增多后再引入 DI 容器。

### 为什么使用异步 I/O

文件操作使用 `async/await`，避免在较大的数据文件或磁盘较慢时长时间阻塞 UI 线程。仓储内部再有写锁，保证并发触发的保存按顺序执行。

## 6. 取舍与边界

- 当前数据量按个人待办设计，加载时一次性读入内存；十万级任务应改用数据库和分页查询。
- 自动保存是异步触发的；应用在极短时间内被强制终止时，最后一次写入可能尚未完成。
- 编辑草稿目前不做本地自动草稿，关闭窗口前不会单独提示未保存修改。
- 优先级只用于展示，没有自动排序；这是为了让用户创建的先后顺序保持稳定。
- 没有提醒通知、重复任务、标签和云同步，这些都属于下一阶段能力。

## 7. 推荐扩展路线

1. 增加分类或标签：扩展 Model，并把标签筛选加入 `MatchesCurrentView`。
2. 增加排序策略：把排序抽成 `ITodoSorter`，避免扩大页面 ViewModel。
3. 增加提醒：新增 `IReminderService`，由操作系统通知适配器实现。
4. 改为 SQLite：实现 `SqliteTodoRepository`，界面和 ViewModel 无需感知。
5. 云同步：在 Repository 后增加同步协调层，并处理冲突和离线队列。
6. 增加测试：对编辑草稿、筛选规则和仓储原子写入建立单元测试。
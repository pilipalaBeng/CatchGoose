# CatchGoose · 抓大鹅（自用版）

Unity 2022.3 LTS · 3D 层叠消除 · 自用、无广告、无内购、无计时。

> 玩法本质 = 3D 版「羊了个羊」：锅里堆着一堆物件，点走压在表面的，凑齐三个同款进托盘消除。
> 和原版的区别只有一条：**这是给自己和对象两个人玩着乐的，不靠恶心人赚钱。**

## 跑起来

1. Unity Hub 打开本目录（2022.3.62f1）
2. 打开 `Assets/Scenes/Gameplay.unity`
3. 场景已搭好（菜单 `CatchGoose/一键搭建场景` 可重建）
4. Play

## 玩法

- 点物件 → 飞进下方托盘
- 托盘里凑齐 3 个同款 → 消除
- 托盘塞满 8 格还没凑齐 → 失败
- 道具每关重置，不看广告：撤销 3 / 洗牌 2 / 取出 1

原型阶段物件是色块方块，美术还没换。

## 难度旋钮

关卡是 ScriptableObject（`Assets/Data/Level_*.asset`），核心就三个数：

| 参数 | 含义 |
| --- | --- |
| `typeCount` | 物件种类数，越大越难 |
| `traySlots` | 托盘槽位，越小越难 |
| `layers` | 堆叠层数，越深被压住的越多 |
| `scatter` | 同组三个物件在解锁序列上的打散度，0 最友好 |

难度比值 = `typeCount / traySlots`。**0.75 左右轻松，1.7 吃力**。原版恶心人的地方是把这个比值顶到 2.1+ 且随机必死，这里不上这套。

内置 5 关：L1 36 物件（11% 被压）→ L5 108 物件（44% 被压）。

## 结构

```
Assets/Scripts/
  Core/  Board.cs           射线拾取 + 遮挡判定
         GameManager.cs     主循环、消除、道具、关卡切换
         Tray.cs            七/八槽托盘
         ItemEntity.cs      单个物件
         LevelGenerator.cs  关卡生成
         Tween.cs           自写协程补间（不引 DOTween）
         GameUI.cs          UI
  Data/  LevelConfig.cs     关卡数值 ScriptableObject
  Editor/SceneBuilder.cs    一键搭场景
```

## 踩过的坑

- **Cylinder primitive 的 CapsuleCollider 会把 height 强制撑到 ≥ 2×radius**。锅底座 scale=(6.2, 0.35, 6.2) 就变成一个从 y=-0.6 顶到 y≈3 的隐形大胶囊，斜俯视角下把锅里所有物件全挡住 → 表现为「画面正常但点不到东西」。
  **原则：纯装饰物一律不留碰撞体。**
- 自写协程补间时，每个 `while` 里都得判 `transform` 还在不在。物件飞一半被消除销毁就会 `MissingReferenceException`。

## License

MIT

# Implementation tips — авторесайз RenderTexture под размер 3D Viewport

Проект: **AlpacaEngine** (raylib-cs + ImGuiNET + rlImGui_cs)
Место доработки: `src/Program.cs`

---

## TL;DR

Рендер-текстура вьюпорта (`RenderTexture2D`) пересоздаётся **только когда размер панели
реально изменился на пороговую величину**, а не каждый кадр. Измерение делает ImGui
в конце кадра, применение — в начале следующего. Поверх добавляется селектор качества:
`0.5x (fast) / 1x / DPI scale (native)`.

---

## 1. Проблема

Кадр сейчас устроен так:

```
STAGE 1: рендер 3D-сцены в viewRenderTexture (жёстко 800x600, Program.cs:38)
STAGE 2: ImGui — панель "3D Viewport" рисует RT через ImageRenderTextureFit
         (Program.cs:121), фактический размер панели (Program.cs:117) не используется
```

Следствия:
- при ресайзе панели картинка тянется/леттербоксится вместо «родного» разрешения;
- на HiDPI-экранах (`HighDpiWindow`, Program.cs:13) картинка мылится: RT в пикселях
  меньше фактического буфера окна.

---

## 2. Ключевая идея: измеряй в конце кадра — применяй в начале следующего

ImGui сообщает доступный размер панели только во время своего прохода (STAGE 2),
а `BeginTextureMode` нужен раньше (STAGE 1). Поэтому:

```
┌─ кадр N
│  STAGE 0.5  - применяем отложенный размер: Unload/Load RT (вне любых FBO!)
│  STAGE 1    - рендер сцены в RT актуального размера
│  STAGE 2    - ImGui: GetContentRegionAvail() -> pendingSize (просто запоминаем)
└─ конец кадра
```

Задержка в 1 кадр при 60 FPS незаметна — это стандартный паттерн editors.

Почему не пересоздавать RT сразу при измерении: рендер этого кадра уже отрисован в
старый RT; смена размера «на лету» посреди кадра оставляет FBO-стек в неконсистентном
состоянии. Только отложенное применение.

---

## 3. Шаг 1 — состояние (рядом с созданием камеры, ~Program.cs:36-38)

```csharp
// ---- render texture для 3D Viewport (стартовый размер — заглушка) ----
var viewRenderTexture = Raylib.LoadRenderTexture(800, 600);
int rtWidth  = 800;
int rtHeight = 600;
Vector2 pendingSize = new(800, 600);

// ---- селектор качества рендера ----
int resScaleIndex = 1; // 0 = 0.5x, 1 = 1x, 2 = DPI scale
string[] resScaleLabels = { "0.5x (fast)", "1x", "DPI scale (native)" };
```

Флаг «dirty» не нужен: стадия применения каждый кадр просто сравнивает желаемый
размер с текущим и решает сама.

---

## 4. Шаг 2 — применение размера (новый STAGE 0.5, до `BeginTextureMode`)

```csharp
// ============================================================
// STAGE 0.5: Recreate viewport RenderTexture if panel size changed
// ============================================================
float resScale = resScaleIndex switch
{
    0 => 0.5f,
    1 => 1f,
    _ => Raylib.GetWindowScaleDPI().X, // HiDPI native
};

int targetW = Math.Clamp((int)(pendingSize.X * resScale), 64, 4096);
int targetH = Math.Clamp((int)(pendingSize.Y * resScale), 64, 4096);

// Гистерезис: не пересоздаём FBO при изменениях меньше 8px
// (drag сплиттера, dock-анимации) — иначе микро-фризы на драйверах GPU
if (Math.Abs(targetW - rtWidth) >= 8 || Math.Abs(targetH - rtHeight) >= 8)
{
    Raylib.UnloadRenderTexture(viewRenderTexture);
    viewRenderTexture = Raylib.LoadRenderTexture(targetW, targetH);
    rtWidth = targetW;
    rtHeight = targetH;
}
```

Важно: код обязан выполняться **вне** `BeginTextureMode`/`BeginDrawing` — raylib
не умеет пересоздавать FBO, пока он вложен в другой активный FBO.

---

## 5. Шаг 3 — измерение и контрол качества в панели (Program.cs:113-123)

```csharp
// PANEL C: 3D Viewport
ImGui.Begin("3D Viewport");

// Контролы рисуем СВЕРХУ. Правило: мерить место непосредственно ПЕРЕД
// отрисовкой текстуры и после всех виджетов — тогда size == область картинки.
ImGui.SetNextItemWidth(180);
ImGui.Combo("Editor 3D Resolution", ref resScaleIndex, resScaleLabels, resScaleLabels.Length);

if (!ImGui.IsWindowCollapsed())
{
    var avail = ImGui.GetContentRegionAvail();
    if (avail.X >= 16 && avail.Y >= 16)
        pendingSize = avail;
}

rlImGui.ImageRenderTextureFit(viewRenderTexture, false);

ImGui.End();
```

`ImageRenderTextureFit` сам вписывает RT в доступную область с сохранением аспекта.
Когда аспект RT совпадает с аспектом панели (а теперь он совпадает почти всегда),
картинка заполняет панель целиком, без полей.

---

## 6. Шаг 4 — убрать отладочный мусор

Панель "Assets" с `DragFloat2("viewport size", ref regionAvail)`
(Program.cs:125-127) больше не нужна — размер теперь измеряется автоматически.

---

## 7. DPI и HiDPI (Retina)

- `GetContentRegionAvail()` возвращает размер **в логических points**,
  `LoadRenderTexture` принимает **пиксели**. На macOS с `HighDpiWindow` DPI-фактор
  обычно 2, т.е. буфер окна в 4 раза больше логической области.
- Режим `1x` = RT размером с панель в points → рендер дешёвый, но GL его
  растягивает ×2 → слегка мыльно.
- Режим `DPI scale` = avail × `GetWindowScaleDPI().X` → RT ровно с буфером окна →
  нативная чёткость.
- Режим `0.5x` — «boost FPS» для тяжёлых сцен.
- `GetWindowScaleDPI()` нужно читать **каждый кадр** (окно можно перетащить на
  монитор с другим DPI). Константа `dpiScale` из Main (Program.cs:18-19) годится
  для стиля ImGui, но не для RT.

---

## 8. Подводные камни (чек-лист)

- [ ] Пересоздание FBO только вне `BeginTextureMode`/`BeginDrawing`.
- [ ] `UnloadRenderTexture` и `LoadRenderTexture` всегда парой — иначе утечка VRAM.
- [ ] `Math.Clamp(..., 64, 4096)`: при 0 или отрицательных размерах (свернутая
      панель, вырожденный layout) raylib создаст битый FBO, драйвер может упасть.
- [ ] Порог 8px обязателен: без него каждый кадр drag-ресайза = realloc FBO на GPU.
- [ ] Не использовать `ImGui.GetWindowSize()`/`GetWindowPos()` — включают декорации
      окна. Нужен `GetContentRegionAvail()` после отрисовки контролов.
- [ ] Задержка в 1 кадр — норма, не «баг» (см. раздел 2).
- [ ] После пересоздания RT меняется `texture.Id`. `rlImGui.ImageRenderTextureFit`
      читает id из переданной структуры каждый кадр, но если текстура «залипла» —
      искать кэш текстур в rlImGui_cs.
- [ ] Не пытаться ресайзнуть GPU-текстуру «вручную»: `LoadRenderTexture` пересоздаёт
      и color, и depth attachment; в raylib-cs нет безопасного `ResizeRenderTexture`.
- [ ] `imgui.ini` восстанавливает layout между запусками — размеры панелей известны
      с первого кадра, но первый рендер всё равно в стартовый 800×600 (норм).

---

## 9. Как проверить

1. `dotnet run`, потянуть разделитель между панелями — 3D-картинка догоняет размер
   за 1-2 кадра, без чёрных полей (аспект совпадает).
2. Переключить Combo в `0.5x` → картинка мыльная, FPS выше; в `DPI scale` →
   чёткая на Retina.
3. Максимизировать окно — RT дорастает (см. порог 8px: применится, т.к. дельта
   превысит порог).
4. Перетащить окно на монитор с другим DPI в режиме `DPI scale` — пересоздание
   произойдёт автоматически.
5. Отладка: вывести текущий размер RT в панель
   `ImGui.Text($"{viewRenderTexture.Texture.Width}x{viewRenderTexture.Texture.Height}");`
   и сравнить с `pendingSize`.

---

## 10. Альтернативы и trade-offs

| Вариант | Плюсы | Минусы | Когда брать |
|---|---|---|---|
| Только фиксированная настройка (RT не меняется) | ~0 сложности, стабильный рендер | апскейл = мыло; впустую занятая VRAM на маленькой панели | MVP/прототип |
| Только авторесайз (без селектора) | всегда чётко 1:1 | нет контроля FPS на 4K/ретине, всегда нативные издержки | простой editor |
| **Гибрид (рекомендуется)** | чёткость + контроль качества | чуть больше состояния и стадия 0.5 | это и реализуем |

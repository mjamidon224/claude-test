using System.Collections.Immutable;
using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using SpiderSolitaire.Core;
using SpiderSolitaire.Rendering;

namespace SpiderSolitaire;

internal enum HintOutcome
{
    /// <summary>A move was highlighted.</summary>
    Shown,

    /// <summary>No moves, so the stock was highlighted instead.</summary>
    DealSuggested,

    /// <summary>No moves and nothing to deal.</summary>
    NoMoves,
}

/// <summary>
/// The table. It draws the game, plays every change as an animation, and turns mouse
/// input into moves: drag a run onto another column, click a run to send it to the best
/// place, click the stock to deal. The Hint, Undo and Undo All buttons under the score
/// are drawn here too; clicking one raises <see cref="ButtonClicked"/>.
/// </summary>
/// <remarks>
/// The game itself changes instantly; what this shows lags behind while an animation
/// plays. Any new input first jumps the animation to the end, so play never waits on it.
/// </remarks>
internal sealed class BoardView : Control
{
    private const double MoveTime = 190;
    private const double UndoTime = 220;
    private const double FlipTime = 150;
    private const double ReturnTime = 170;
    private const double DealCardTime = 220;
    private const double DealStagger = 45;
    private const double CollectCardTime = 240;
    private const double CollectStagger = 40;
    private const double OpeningCardTime = 260;
    private const double OpeningStagger = 22;
    private const double HintPhaseTime = 650;
    private const double FireworksTime = 8;

    private static readonly Color HintSourceColor = Color.FromArgb(255, 214, 64);
    private static readonly Color HintTargetColor = Color.FromArgb(110, 220, 255);

    private readonly BoardPainter _painter = new();
    private readonly System.Windows.Forms.Timer _frames = new() { Interval = 15 };
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly Fireworks _fireworks = new();
    private readonly Queue<Stage> _pending = new();
    private readonly ToolTip _toolTip = new();

    private Game? _game;
    private Board? _shown;
    private BoardLayout? _layout;
    private Animation? _animation;
    private Bitmap? _staticLayer;
    private bool _staticDirty = true;
    private Press? _press;
    private Drag? _drag;
    private HintDisplay? _hint;
    private int _hintCursor;
    private bool _lastClickActed;
    private BoardButton? _hoverButton;
    private BoardButton? _pressedButton;
    private double _lastFrame;

    public BoardView()
    {
        SetStyle(
            ControlStyles.AllPaintingInWmPaint
            | ControlStyles.OptimizedDoubleBuffer
            | ControlStyles.UserPaint
            | ControlStyles.Opaque
            | ControlStyles.ResizeRedraw,
            true);

        _frames.Tick += OnFrame;
    }

    /// <summary>After any move, deal or undo that changed the game.</summary>
    public event EventHandler? GameChanged;

    /// <summary>As each stage of a change starts to play (or at once, without animation), for sounds.</summary>
    public event EventHandler<StageKind>? StagePlayed;

    /// <summary>The stock was clicked while a column is empty.</summary>
    public event EventHandler? DealRefused;

    /// <summary>A clicked card had nowhere to go.</summary>
    public event EventHandler? MoveRefused;

    /// <summary>Everything shown has caught up with the game.</summary>
    public event EventHandler? AnimationsFinished;

    /// <summary>One of the buttons under the score was clicked while enabled.</summary>
    public event EventHandler<BoardButton>? ButtonClicked;

    public bool AnimationsEnabled { get; set; } = true;

    public CardBackStyle CardBack
    {
        get => _painter.CardBack;
        set
        {
            _painter.CardBack = value;
            InvalidateStatic();
        }
    }

    public TableStyle Table
    {
        get => _painter.Table;
        set
        {
            _painter.Table = value;
            InvalidateStatic();
        }
    }

    private double Now => _clock.Elapsed.TotalMilliseconds;

    private BoardLayout? CurrentLayout
    {
        get
        {
            if (_shown is null)
            {
                return null;
            }

            if (_layout is null || !ReferenceEquals(_layout.Board, _shown) || _layout.ClientSize != ClientSize)
            {
                _layout = new BoardLayout(ClientSize, _shown);
            }

            return _layout;
        }
    }

    /// <summary>Shows a game, dealing it out card by card if <paramref name="animateDeal"/> is set.</summary>
    public void StartGame(Game game, bool animateDeal)
    {
        CancelDrag(animate: false);
        _press = null;
        _hint = null;
        _pending.Clear();
        _animation = null;
        _fireworks.Stop();

        _game = game;
        _shown = game.Board;
        _hintCursor = 0;
        InvalidateStatic();

        if (animateDeal && AnimationsEnabled)
        {
            StartOpeningAnimation();
        }

        Invalidate();
    }

    public void Deal()
    {
        if (!PrepareForAction() || _game!.IsWon)
        {
            return;
        }

        switch (_game.DealBlocker)
        {
            case DealBlocker.EmptyColumn:
                DealRefused?.Invoke(this, EventArgs.Empty);
                return;
            case DealBlocker.StockEmpty:
                return;
        }

        Play(_game.TryDeal()!, null);
    }

    public void Undo()
    {
        if (!PrepareForAction())
        {
            return;
        }

        IReadOnlyList<Stage>? stages = _game!.Undo();
        if (stages is not null)
        {
            Play(stages, null);
        }
    }

    /// <summary>Goes back to the opening deal in one step.</summary>
    public void UndoAll()
    {
        if (!PrepareForAction())
        {
            return;
        }

        IReadOnlyList<Stage>? stages = _game!.UndoAll();
        if (stages is not null)
        {
            Play(stages, null);
        }
    }

    /// <summary>Highlights the next suggested move; pressing again cycles through the others.</summary>
    public HintOutcome ShowHint()
    {
        if (!PrepareForAction())
        {
            return HintOutcome.NoMoves;
        }

        BoardLayout layout = CurrentLayout!;
        IReadOnlyList<Hint> hints = _game!.FindHints();

        if (hints.Count == 0)
        {
            if (!_game.IsWon && _game.DealBlocker == DealBlocker.None)
            {
                _hint = new HintDisplay(layout.NextDealPile, null, Now, IsStock: true);
                EnsureTimer();
                Invalidate();
                return HintOutcome.DealSuggested;
            }

            return HintOutcome.NoMoves;
        }

        Hint hint = hints[_hintCursor++ % hints.Count];
        ImmutableArray<TableauCard> source = _shown!.Columns[hint.FromColumn];
        ImmutableArray<TableauCard> target = _shown.Columns[hint.ToColumn];

        RectangleF sourceRect = RectangleF.Union(layout[source[hint.FromIndex].Card.Id].Rect, layout[source[^1].Card.Id].Rect);
        RectangleF targetRect = target.IsEmpty ? layout.ColumnSlot(hint.ToColumn) : layout[target[^1].Card.Id].Rect;

        _hint = new HintDisplay(sourceRect, targetRect, Now, IsStock: false);
        EnsureTimer();
        Invalidate();
        return HintOutcome.Shown;
    }

    public void Celebrate()
    {
        _fireworks.Start(FireworksTime);
        EnsureTimer();
    }

    /// <summary>Repaints just the score and buttons, for the clock.</summary>
    public void InvalidateScore() => InvalidateControls();

    /// <summary>Jumps any animation to its end, so what is shown matches the game.</summary>
    public void FinishAnimations()
    {
        bool wasAnimating = _animation is not null || _pending.Count > 0;
        _pending.Clear();
        _animation = null;

        if (_game is not null && !ReferenceEquals(_shown, _game.Board))
        {
            _shown = _game.Board;
        }

        if (wasAnimating)
        {
            InvalidateStatic();
            Invalidate();
            AnimationsFinished?.Invoke(this, EventArgs.Empty);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _frames.Dispose();
            _toolTip.Dispose();
            _painter.Dispose();
            _staticLayer?.Dispose();
        }

        base.Dispose(disposing);
    }

    /// <summary>Clears anything in progress before a new action. Returns false if there is no game yet.</summary>
    private bool PrepareForAction()
    {
        if (_game is null)
        {
            return false;
        }

        CancelDrag(animate: false);
        _press = null;
        CancelHint();
        FinishAnimations();
        return true;
    }

    private void Play(IReadOnlyList<Stage> stages, IReadOnlyDictionary<int, RectangleF>? startRects)
    {
        _hintCursor = 0;
        GameChanged?.Invoke(this, EventArgs.Empty);

        if (!AnimationsEnabled)
        {
            foreach (Stage stage in stages)
            {
                StagePlayed?.Invoke(this, stage.Kind);
            }

            _shown = _game!.Board;
            InvalidateStatic();
            Invalidate();
            AnimationsFinished?.Invoke(this, EventArgs.Empty);
            return;
        }

        foreach (Stage stage in stages)
        {
            _pending.Enqueue(stage);
        }

        AdvanceStage(startRects);
    }

    /// <summary>Starts the next queued stage that has anything to animate, or reports that all are done.</summary>
    private void AdvanceStage(IReadOnlyDictionary<int, RectangleF>? startRects)
    {
        while (_pending.Count > 0)
        {
            Stage stage = _pending.Dequeue();
            BoardLayout from = CurrentLayout!;
            _shown = stage.Board;
            BoardLayout to = CurrentLayout!;

            List<Tween> tweens = BuildTweens(stage.Kind, from, to, startRects);
            startRects = null;
            StagePlayed?.Invoke(this, stage.Kind);

            if (tweens.Count > 0)
            {
                StartAnimation(tweens);
                return;
            }
        }

        _animation = null;
        InvalidateStatic();
        Invalidate();
        AnimationsFinished?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>A tween for every card whose place or face differs between two layouts, timed for the kind of stage.</summary>
    private static List<Tween> BuildTweens(StageKind kind, BoardLayout from, BoardLayout to, IReadOnlyDictionary<int, RectangleF>? startRects)
    {
        List<Tween> tweens = new();

        foreach (Placement next in to.Placements)
        {
            if (!from.TryGet(next.Card.Id, out Placement previous))
            {
                continue;
            }

            RectangleF start = startRects is not null && startRects.TryGetValue(next.Card.Id, out RectangleF dragged) ? dragged : previous.Rect;
            if (SameRect(start, next.Rect) && previous.FaceUp == next.FaceUp)
            {
                continue;
            }

            (double delay, double duration) = kind switch
            {
                StageKind.Deal => (next.Pile == Pile.Column ? next.PileIndex * DealStagger : 0, DealCardTime),
                StageKind.CompleteSuit => (next.Pile == Pile.Foundation ? next.Position * CollectStagger : 0, CollectCardTime),
                StageKind.Flip => (0, FlipTime),
                StageKind.Undo => (0, UndoTime),
                _ => (0, MoveTime),
            };

            tweens.Add(new Tween(next.Card, start, next.Rect, previous.FaceUp, next.FaceUp, delay, duration, previous.Z, next.Z));
        }

        return tweens;
    }

    /// <summary>A new game's cards flying out of the stock, a row at a time, as if dealt by hand.</summary>
    private void StartOpeningAnimation()
    {
        BoardLayout layout = CurrentLayout!;
        RectangleF origin = layout.NextDealPile;
        List<Tween> tweens = new();

        foreach (Placement placement in layout.Placements)
        {
            if (placement.Pile != Pile.Column)
            {
                continue;
            }

            int order = placement.Position * Board.ColumnCount + placement.PileIndex;
            tweens.Add(new Tween(placement.Card, origin, placement.Rect, false, placement.FaceUp, order * OpeningStagger, OpeningCardTime, order, placement.Z));
        }

        StagePlayed?.Invoke(this, StageKind.Deal);
        StartAnimation(tweens);
    }

    private void StartAnimation(List<Tween> tweens)
    {
        _animation = new Animation(tweens, Now);
        InvalidateStatic();
        EnsureTimer();
        Invalidate();
    }

    private void EnsureTimer()
    {
        if (!_frames.Enabled)
        {
            _lastFrame = Now;
            _frames.Start();
        }
    }

    private void OnFrame(object? sender, EventArgs e)
    {
        double now = Now;
        double elapsed = Math.Min(100, now - _lastFrame);
        _lastFrame = now;

        if (_animation is not null && now >= _animation.End)
        {
            _animation = null;
            AdvanceStage(null);
        }

        if (_hint is not null && now - _hint.Start >= HintPhaseTime * 2)
        {
            _hint = null;
        }

        _fireworks.Update(elapsed / 1000, ClientSize);

        if (_animation is null && _hint is null && !_fireworks.IsActive)
        {
            _frames.Stop();
        }

        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics graphics = e.Graphics;
        BoardLayout? layout = CurrentLayout;
        if (_game is null || layout is null || ClientSize.Width <= 0 || ClientSize.Height <= 0)
        {
            if (ClientSize.Width > 0 && ClientSize.Height > 0)
            {
                _painter.PaintTable(graphics, ClientSize);
            }

            return;
        }

        double now = Now;

        if (_staticDirty || _staticLayer is null || _staticLayer.Size != ClientSize)
        {
            RenderStaticLayer(layout);
        }

        graphics.DrawImageUnscaled(_staticLayer!, 0, 0);
        graphics.InterpolationMode = InterpolationMode.HighQualityBilinear;
        graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

        if (_animation is not null)
        {
            PaintAnimation(graphics, layout, now);
        }

        if (_drag is not null)
        {
            PaintDrag(graphics, layout);
        }

        if (_hint is not null)
        {
            PaintHint(graphics, layout, now);
        }

        _fireworks.Paint(graphics, layout.CardSize.Width);
    }

    /// <summary>Draws everything that is not moving into a bitmap, so animation frames only redraw the moving cards.</summary>
    private void RenderStaticLayer(BoardLayout layout)
    {
        if (_staticLayer is null || _staticLayer.Size != ClientSize)
        {
            _staticLayer?.Dispose();
            _staticLayer = new Bitmap(ClientSize.Width, ClientSize.Height, PixelFormat.Format32bppPArgb);
        }

        HashSet<int> moving = new();
        if (_animation is not null)
        {
            moving.UnionWith(_animation.CardIds);
        }

        if (_drag is not null)
        {
            moving.UnionWith(_drag.Cards.Select(card => card.Card.Id));
        }

        using Graphics graphics = Graphics.FromImage(_staticLayer);
        _painter.PaintStatic(graphics, layout, moving, new ScoreInfo(_game!.Score, _game.Moves, _game.Elapsed), ButtonStates());
        _staticDirty = false;
    }

    private void PaintAnimation(Graphics graphics, BoardLayout layout, double now)
    {
        double elapsed = now - _animation!.Start;
        float cardWidth = layout.CardSize.Width;

        // Cards still waiting their turn keep their old stacking order; moving and landed
        // cards go on top in the order they set off, so later arrivals land on earlier ones.
        foreach (Tween tween in _animation.Tweens.Where(t => elapsed < t.Delay).OrderBy(t => t.FromZ))
        {
            _painter.PaintCard(graphics, tween.Card, tween.From, tween.FromFaceUp);
        }

        foreach (Tween tween in _animation.Tweens.Where(t => elapsed >= t.Delay).OrderBy(t => t.Delay).ThenBy(t => t.ToZ))
        {
            double progress = Math.Clamp((elapsed - tween.Delay) / tween.Duration, 0, 1);
            float eased = (float)(1 - Math.Pow(1 - progress, 3));
            RectangleF rect = new(
                tween.From.X + (tween.To.X - tween.From.X) * eased,
                tween.From.Y + (tween.To.Y - tween.From.Y) * eased,
                tween.To.Width,
                tween.To.Height);

            bool faceUp = tween.ToFaceUp;
            float squash = 1f;
            if (tween.FromFaceUp != tween.ToFaceUp && progress < 1)
            {
                // Turning over: narrow to an edge showing the old side, widen showing the new.
                faceUp = progress < 0.5 ? tween.FromFaceUp : tween.ToFaceUp;
                squash = (float)Math.Abs(1 - progress * 2);
            }

            if (progress < 1 && !SameRect(tween.From, tween.To))
            {
                _painter.PaintShadow(graphics, rect, cardWidth);
            }

            _painter.PaintCard(graphics, tween.Card, rect, faceUp, squash);
        }
    }

    private void PaintDrag(Graphics graphics, BoardLayout layout)
    {
        Drag drag = _drag!;
        RectangleF first = drag.RectOf(0, layout.CardSize);
        RectangleF last = drag.RectOf(drag.Cards.Count - 1, layout.CardSize);
        _painter.PaintShadow(graphics, RectangleF.Union(first, last), layout.CardSize.Width);

        for (int i = 0; i < drag.Cards.Count; i++)
        {
            _painter.PaintCard(graphics, drag.Cards[i].Card, drag.RectOf(i, layout.CardSize), faceUp: true);
        }
    }

    private void PaintHint(Graphics graphics, BoardLayout layout, double now)
    {
        HintDisplay hint = _hint!;
        double elapsed = now - hint.Start;
        float cardWidth = layout.CardSize.Width;

        if (hint.IsStock)
        {
            // Blink the stock twice.
            if ((int)(elapsed / (HintPhaseTime / 2)) % 2 == 0)
            {
                BoardPainter.PaintHighlight(graphics, hint.Source, cardWidth, HintTargetColor);
            }

            return;
        }

        if (elapsed < HintPhaseTime)
        {
            BoardPainter.PaintHighlight(graphics, hint.Source, cardWidth, HintSourceColor);
        }
        else if (hint.Target is { } target)
        {
            BoardPainter.PaintHighlight(graphics, target, cardWidth, HintTargetColor);
        }
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button != MouseButtons.Left || _game is null)
        {
            return;
        }

        // The second click of a double-click is ignored if the first one already did
        // something: it would otherwise move whatever card the first click left under the
        // pointer, or deal a second row from the stock.
        if (e.Clicks > 1 && _lastClickActed)
        {
            _lastClickActed = false;
            return;
        }

        _lastClickActed = false;
        CancelHint();
        FinishAnimations();

        if (CurrentLayout is not { } layout)
        {
            return;
        }

        // Cards come first: a column long enough to reach the bottom row covers the buttons.
        if (_game.IsWon || layout.HitTest(e.Location) is not { } hit)
        {
            if (layout.ButtonAt(e.Location) is { } button && IsEnabled(button))
            {
                _pressedButton = button;
                InvalidateControls();
            }

            return;
        }

        if (hit.Pile == Pile.Stock)
        {
            int movesBefore = _game.Moves;
            Deal();
            _lastClickActed = _game.Moves != movesBefore;
        }
        else if (hit.Pile == Pile.Column && hit.Position >= 0 && _shown!.CanPickUp(hit.PileIndex, hit.Position))
        {
            _press = new Press(hit.PileIndex, hit.Position, e.Location);
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);

        if ((e.Button & MouseButtons.Left) == 0)
        {
            // The button came up somewhere we did not hear about (another window, say).
            _press = null;
            CancelDrag(animate: true);
            if (_pressedButton is not null)
            {
                _pressedButton = null;
                InvalidateControls();
            }

            UpdateHover(e.Location);
            return;
        }

        if (_press is null)
        {
            return;
        }

        if (_drag is null)
        {
            Size threshold = SystemInformation.DragSize;
            if (Math.Abs(e.X - _press.Location.X) <= threshold.Width / 2 && Math.Abs(e.Y - _press.Location.Y) <= threshold.Height / 2)
            {
                return;
            }

            BeginDrag(_press);
        }

        _drag!.Pointer = e.Location;
        Invalidate();
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (e.Button != MouseButtons.Left)
        {
            return;
        }

        if (_pressedButton is { } pressed)
        {
            _pressedButton = null;
            InvalidateControls();
            if (CurrentLayout?.ButtonAt(e.Location) == pressed && IsEnabled(pressed))
            {
                ButtonClicked?.Invoke(this, pressed);
            }

            return;
        }

        Press? press = _press;
        _press = null;

        if (_drag is not null)
        {
            Drop();
        }
        else if (press is not null)
        {
            ClickMove(press);
        }
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        UpdateHover(null);
    }

    protected override void OnMouseCaptureChanged(EventArgs e)
    {
        base.OnMouseCaptureChanged(e);

        // Capture is also released by an ordinary mouse-up, which handles the drop itself;
        // check once that has run, and only put the cards back if a drag is still going.
        if (IsHandleCreated)
        {
            BeginInvoke(() =>
            {
                if (_drag is not null && (MouseButtons & MouseButtons.Left) == 0)
                {
                    _press = null;
                    CancelDrag(animate: true);
                }
            });
        }
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        CancelDrag(animate: false);
        _press = null;
        _hint = null;
        FinishAnimations();
        InvalidateStatic();
    }

    private void BeginDrag(Press press)
    {
        BoardLayout layout = CurrentLayout!;
        ImmutableArray<TableauCard> column = _shown!.Columns[press.Column];
        RectangleF first = layout[column[press.Index].Card.Id].Rect;

        List<(Card Card, SizeF Offset)> cards = new();
        for (int i = press.Index; i < column.Length; i++)
        {
            RectangleF rect = layout[column[i].Card.Id].Rect;
            cards.Add((column[i].Card, new SizeF(rect.X - first.X, rect.Y - first.Y)));
        }

        _drag = new Drag(press.Column, press.Index, cards, new SizeF(press.Location.X - first.X, press.Location.Y - first.Y))
        {
            Pointer = press.Location,
        };

        InvalidateStatic();
    }

    private void Drop()
    {
        Drag drag = _drag!;
        _drag = null;
        BoardLayout layout = CurrentLayout!;
        Dictionary<int, RectangleF> rects = drag.Rects(layout.CardSize);

        int? target = FindDropTarget(drag, layout);
        IReadOnlyList<Stage>? stages = target is null ? null : _game!.TryMove(drag.Column, drag.Index, target.Value);

        if (stages is not null)
        {
            Play(stages, rects);
        }
        else
        {
            ReturnCards(rects);
        }
    }

    /// <summary>
    /// The legal column the dropped cards overlap most. Anywhere in a column's strip
    /// counts, not just over its last card, which makes drops forgiving.
    /// </summary>
    private int? FindDropTarget(Drag drag, BoardLayout layout)
    {
        RectangleF dropped = drag.RectOf(0, layout.CardSize);
        int? best = null;
        float bestOverlap = 0;

        for (int column = 0; column < Board.ColumnCount; column++)
        {
            if (column == drag.Column || !_shown!.CanMove(drag.Column, drag.Index, column))
            {
                continue;
            }

            RectangleF strip = RectangleF.FromLTRB(layout.ColumnLeft(column), 0, layout.ColumnLeft(column) + layout.CardSize.Width, ClientSize.Height);
            RectangleF overlap = RectangleF.Intersect(strip, dropped);
            float area = overlap.Width * overlap.Height;
            if (area > bestOverlap)
            {
                bestOverlap = area;
                best = column;
            }
        }

        return best;
    }

    private void ClickMove(Press press)
    {
        int? target = _shown!.FindBestTarget(press.Column, press.Index);
        IReadOnlyList<Stage>? stages = target is null ? null : _game!.TryMove(press.Column, press.Index, target.Value);

        if (stages is null)
        {
            MoveRefused?.Invoke(this, EventArgs.Empty);
            return;
        }

        _lastClickActed = true;
        Play(stages, null);
    }

    private void CancelDrag(bool animate)
    {
        if (_drag is null)
        {
            return;
        }

        Drag drag = _drag;
        _drag = null;

        if (animate && CurrentLayout is { } layout)
        {
            ReturnCards(drag.Rects(layout.CardSize));
        }
        else
        {
            InvalidateStatic();
            Invalidate();
        }
    }

    /// <summary>Slides dragged cards back to where they came from.</summary>
    private void ReturnCards(Dictionary<int, RectangleF> from)
    {
        BoardLayout layout = CurrentLayout!;
        if (!AnimationsEnabled)
        {
            InvalidateStatic();
            Invalidate();
            return;
        }

        List<Tween> tweens = from
            .Select(entry => (Start: entry.Value, Home: layout[entry.Key]))
            .Select(card => new Tween(card.Home.Card, card.Start, card.Home.Rect, true, card.Home.FaceUp, 0, ReturnTime, card.Home.Z, card.Home.Z))
            .ToList();

        StartAnimation(tweens);
    }

    private bool IsEnabled(BoardButton button) =>
        _game is { } game && (button == BoardButton.Hint ? !game.IsWon : game.CanUndo);

    private IReadOnlyList<BoardButtonState> ButtonStates() => Enum.GetValues<BoardButton>()
        .Select(button => new BoardButtonState(button, IsEnabled(button), button == _hoverButton, button == _pressedButton))
        .ToList();

    /// <summary>Tracks which button the pointer is over, for the hover look and the tooltip.</summary>
    private void UpdateHover(Point? location)
    {
        BoardButton? over = null;
        if (location is { } point && _drag is null && CurrentLayout is { } layout && layout.HitTest(point) is null)
        {
            over = layout.ButtonAt(point);
        }

        if (over == _hoverButton)
        {
            return;
        }

        _hoverButton = over;
        _toolTip.SetToolTip(this, over switch
        {
            BoardButton.Hint => "Show a move worth making (H)",
            BoardButton.Undo => "Take back the last move (Ctrl+Z)",
            BoardButton.UndoAll => "Go back to the start of this game",
            _ => null,
        });

        InvalidateControls();
    }

    /// <summary>Redraws the static layer but only repaints the score and buttons.</summary>
    private void InvalidateControls()
    {
        _staticDirty = true;
        if (CurrentLayout is { } layout)
        {
            Invalidate(Rectangle.Inflate(Rectangle.Round(layout.ControlsArea), 2, 2));
        }
    }

    private void CancelHint()
    {
        if (_hint is not null)
        {
            _hint = null;
            Invalidate();
        }
    }

    private void InvalidateStatic()
    {
        _staticDirty = true;
        Invalidate();
    }

    private static bool SameRect(RectangleF a, RectangleF b) =>
        Math.Abs(a.X - b.X) < 0.5f && Math.Abs(a.Y - b.Y) < 0.5f;

    private sealed record Tween(Card Card, RectangleF From, RectangleF To, bool FromFaceUp, bool ToFaceUp, double Delay, double Duration, int FromZ, int ToZ);

    private sealed class Animation
    {
        public Animation(List<Tween> tweens, double start)
        {
            Tweens = tweens;
            Start = start;
            End = start + tweens.Max(tween => tween.Delay + tween.Duration);
            CardIds = tweens.Select(tween => tween.Card.Id).ToHashSet();
        }

        public List<Tween> Tweens { get; }

        public double Start { get; }

        public double End { get; }

        public HashSet<int> CardIds { get; }
    }

    private sealed record Press(int Column, int Index, Point Location);

    private sealed record HintDisplay(RectangleF Source, RectangleF? Target, double Start, bool IsStock);

    /// <summary>Cards being dragged, kept at the spacing they had in their column.</summary>
    private sealed class Drag
    {
        public Drag(int column, int index, List<(Card Card, SizeF Offset)> cards, SizeF grab)
        {
            Column = column;
            Index = index;
            Cards = cards;
            Grab = grab;
        }

        public int Column { get; }

        public int Index { get; }

        public List<(Card Card, SizeF Offset)> Cards { get; }

        /// <summary>Where the pointer took hold of the first card, relative to its corner.</summary>
        public SizeF Grab { get; }

        public Point Pointer { get; set; }

        public RectangleF RectOf(int index, Size cardSize) => new(
            Pointer.X - Grab.Width + Cards[index].Offset.Width,
            Pointer.Y - Grab.Height + Cards[index].Offset.Height,
            cardSize.Width,
            cardSize.Height);

        public Dictionary<int, RectangleF> Rects(Size cardSize) =>
            Cards.Select((card, i) => (card.Card.Id, Rect: RectOf(i, cardSize))).ToDictionary(entry => entry.Id, entry => entry.Rect);
    }
}

using System.Collections.Immutable;
using SpiderSolitaire.Core;

namespace SpiderSolitaire.Rendering;

internal enum Pile
{
    Column,
    Stock,
    Foundation,
}

/// <summary>Where one card is drawn. <see cref="Position"/> is its index within the pile; <see cref="Z"/> is its paint order.</summary>
internal readonly record struct Placement(Card Card, RectangleF Rect, bool FaceUp, Pile Pile, int PileIndex, int Position, int Z);

internal readonly record struct HitResult(Pile Pile, int PileIndex, int Position);

/// <summary>The buttons under the score, left to right.</summary>
internal enum BoardButton
{
    Hint,
    Undo,
    UndoAll,
}

/// <summary>
/// The geometry of the table for one board at one window size: card size, where every
/// card sits, and what lies under a point.
/// </summary>
/// <remarks>
/// Like the Windows game, the ten columns run across the top, finished suits stack in
/// the bottom left, the stock sits in the bottom right with one card per remaining deal,
/// and the score, with the Hint, Undo and Undo All buttons under it, sits between them.
/// Long columns squeeze their spacing to stay on screen.
/// </remarks>
internal sealed class BoardLayout
{
    public const float CardAspect = 1.4f;
    private const float GapRatio = 0.14f;
    private const float FaceDownSpacing = 0.1f;
    private const float FaceUpSpacing = 0.24f;
    private const float MinFaceDownSpacing = 0.04f;
    private const float MinFaceUpSpacing = 0.15f;
    private const float StockSpacing = 0.17f;
    private const float FoundationSpacing = 0.3f;
    private const int StockPiles = 5;
    private const float ControlsWidth = 3.4f;
    private const float PanelHeight = 0.34f;
    private const float ButtonHeight = 0.34f;
    private const float ControlsSpacing = 0.08f;

    private readonly Dictionary<int, Placement> _byId = new();
    private readonly List<Placement> _ordered = new();
    private readonly float _left;

    public BoardLayout(Size clientSize, Board board)
    {
        ClientSize = clientSize;
        Board = board;

        float width = Math.Max(1, clientSize.Width);
        float height = Math.Max(1, clientSize.Height);

        // As large as the width allows, unless that leaves no room for the columns to grow.
        float byWidth = width / (Board.ColumnCount + (Board.ColumnCount + 1) * GapRatio);
        float byHeight = height / (CardAspect * 3.3f + 3 * GapRatio);
        int cardWidth = (int)Math.Max(20, Math.Floor(Math.Min(byWidth, byHeight)));
        CardSize = new Size(cardWidth, (int)Math.Round(cardWidth * CardAspect));
        Gap = cardWidth * GapRatio;

        float tableauWidth = Board.ColumnCount * cardWidth + (Board.ColumnCount - 1) * Gap;
        _left = (float)Math.Floor((width - tableauWidth) / 2);
        Right = _left + tableauWidth;
        Top = (float)Math.Round(Gap);
        BottomRowY = (float)Math.Floor(height - Gap - CardSize.Height);

        int z = 0;
        PlaceFoundations(ref z);
        PlaceStock(ref z);
        PlaceColumns(ref z);
    }

    public Size ClientSize { get; }

    public Board Board { get; }

    public Size CardSize { get; }

    public float Gap { get; }

    public float Top { get; }

    public float Right { get; }

    public float BottomRowY { get; }

    /// <summary>Every card in paint order.</summary>
    public IReadOnlyList<Placement> Placements => _ordered;

    public Placement this[int cardId] => _byId[cardId];

    public bool TryGet(int cardId, out Placement placement) => _byId.TryGetValue(cardId, out placement);

    public float ColumnLeft(int column) => _left + column * (CardSize.Width + Gap);

    /// <summary>The outline shown for an empty column, and where its first card goes.</summary>
    public RectangleF ColumnSlot(int column) => new(ColumnLeft(column), Top, CardSize.Width, CardSize.Height);

    /// <summary>Where the stock pile for one remaining deal sits, 0 being the last deal and leftmost.</summary>
    public RectangleF StockPile(int deal) =>
        new(Right - CardSize.Width - (StockPiles - 1 - deal) * CardSize.Width * StockSpacing, BottomRowY, CardSize.Width, CardSize.Height);

    /// <summary>The pile the next row is dealt from, and where a new game's cards fly out of.</summary>
    public RectangleF NextDealPile => StockPile(Math.Clamp(Board.RowsLeftToDeal - 1, 0, StockPiles - 1));

    public RectangleF FoundationPile(int index) =>
        new(_left + index * CardSize.Width * FoundationSpacing, BottomRowY, CardSize.Width, CardSize.Height);

    /// <summary>
    /// The score box: one line, centred under the columns between the finished suits and
    /// the stock, with the buttons beneath it. Together they take the height of a card.
    /// </summary>
    public RectangleF ScorePanel
    {
        get
        {
            float width = CardSize.Width * ControlsWidth;
            float height = CardSize.Height * (PanelHeight + ControlsSpacing + ButtonHeight);
            float centre = (_left + Right) / 2;
            return new RectangleF(
                (float)Math.Round(centre - width / 2),
                (float)Math.Round(BottomRowY + (CardSize.Height - height) / 2),
                (float)Math.Round(width),
                (float)Math.Round(CardSize.Height * PanelHeight));
        }
    }

    /// <summary>The bottom area that holds the score and buttons, for repainting just that part.</summary>
    public RectangleF ControlsArea => RectangleF.Union(ScorePanel, ButtonRect(BoardButton.UndoAll));

    public RectangleF ButtonRect(BoardButton button)
    {
        RectangleF panel = ScorePanel;
        float gap = (float)Math.Round(CardSize.Width * 0.12f);
        float width = (float)Math.Floor((panel.Width - 2 * gap) / 3);
        return new RectangleF(
            panel.Left + (int)button * (width + gap),
            (float)Math.Round(panel.Bottom + CardSize.Height * ControlsSpacing),
            width,
            (float)Math.Round(CardSize.Height * ButtonHeight));
    }

    public BoardButton? ButtonAt(PointF point)
    {
        foreach (BoardButton button in Enum.GetValues<BoardButton>())
        {
            if (ButtonRect(button).Contains(point))
            {
                return button;
            }
        }

        return null;
    }

    /// <summary>The topmost card or pile under a point, or null for bare table.</summary>
    public HitResult? HitTest(PointF point)
    {
        for (int column = 0; column < Board.ColumnCount; column++)
        {
            RectangleF slot = ColumnSlot(column);
            if (point.X < slot.Left || point.X >= slot.Right)
            {
                continue;
            }

            ImmutableArray<TableauCard> cards = Board.Columns[column];
            for (int i = cards.Length - 1; i >= 0; i--)
            {
                if (_byId[cards[i].Card.Id].Rect.Contains(point))
                {
                    return new HitResult(Pile.Column, column, i);
                }
            }

            if (cards.IsEmpty && slot.Contains(point))
            {
                return new HitResult(Pile.Column, column, -1);
            }
        }

        for (int deal = 0; deal < Board.RowsLeftToDeal; deal++)
        {
            if (StockPile(deal).Contains(point))
            {
                return new HitResult(Pile.Stock, 0, -1);
            }
        }

        return null;
    }

    private void PlaceFoundations(ref int z)
    {
        for (int i = 0; i < Board.Foundations.Length; i++)
        {
            RectangleF rect = FoundationPile(i);
            ImmutableArray<Card> suit = Board.Foundations[i];
            for (int position = 0; position < suit.Length; position++)
            {
                Add(new Placement(suit[position], rect, true, Pile.Foundation, i, position, z++));
            }
        }
    }

    private void PlaceStock(ref int z)
    {
        // Ten cards per pile; the last pile (rightmost, on top) is the next row dealt.
        for (int i = 0; i < Board.Stock.Length; i++)
        {
            int deal = Math.Min(i / Board.ColumnCount, StockPiles - 1);
            Add(new Placement(Board.Stock[i], StockPile(deal), false, Pile.Stock, 0, i, z++));
        }
    }

    private void PlaceColumns(ref int z)
    {
        float available = BottomRowY - Gap * 0.5f - Top;

        for (int column = 0; column < Board.ColumnCount; column++)
        {
            ImmutableArray<TableauCard> cards = Board.Columns[column];
            (float down, float up) = Spacing(cards, available);

            float left = ColumnLeft(column);
            float y = Top;
            for (int i = 0; i < cards.Length; i++)
            {
                Add(new Placement(cards[i].Card, new RectangleF(left, (float)Math.Round(y), CardSize.Width, CardSize.Height), cards[i].FaceUp, Pile.Column, column, i, z++));
                y += cards[i].FaceUp ? up : down;
            }
        }
    }

    /// <summary>
    /// The gaps after face-down and face-up cards. A column too long for the space first
    /// tightens its face-up cards, then its face-down ones, down to a floor that keeps the
    /// corner of every face-up card readable; past that it runs off the bottom.
    /// </summary>
    private (float Down, float Up) Spacing(ImmutableArray<TableauCard> cards, float available)
    {
        float height = CardSize.Height;
        float down = height * FaceDownSpacing;
        float up = height * FaceUpSpacing;
        if (cards.Length < 2)
        {
            return (down, up);
        }

        int downCount = 0;
        int upCount = 0;
        for (int i = 0; i < cards.Length - 1; i++)
        {
            if (cards[i].FaceUp)
            {
                upCount++;
            }
            else
            {
                downCount++;
            }
        }

        float room = available - height;
        if (downCount * down + upCount * up <= room)
        {
            return (down, up);
        }

        if (upCount > 0)
        {
            up = Math.Max(height * MinFaceUpSpacing, (room - downCount * down) / upCount);
        }

        if (downCount > 0 && downCount * down + upCount * up > room)
        {
            down = Math.Max(height * MinFaceDownSpacing, (room - upCount * up) / downCount);
        }

        return (down, up);
    }

    private void Add(Placement placement)
    {
        _byId[placement.Card.Id] = placement;
        _ordered.Add(placement);
    }
}

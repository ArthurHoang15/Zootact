namespace Zootact.Core.Domain;

/// <summary>
/// The 8 animal pieces in Zootact, ordered by rank.
/// Higher rank can capture lower rank (except Rat→Elephant special rule).
/// </summary>
public enum PieceType
{
    Rat = 1,      // Can enter river, can capture Elephant from land
    Cat = 2,
    Wolf = 3,     // Called Dog in some variants
    Dog = 4,
    Leopard = 5,
    Tiger = 6,    // Can jump over river
    Lion = 7,     // Can jump over river
    Elephant = 8  // Highest rank, but cannot capture Rat
}

namespace Zootact.Infrastructure.Services;

/// <summary>
/// ELO rating calculator using the standard chess ELO formula.
/// </summary>
public static class EloCalculator
{
    private const int KFactor = 32; // Standard K-factor for online games
    
    /// <summary>
    /// Calculates new ELO ratings after a match.
    /// </summary>
    /// <param name="blueElo">Blue player's current ELO.</param>
    /// <param name="redElo">Red player's current ELO.</param>
    /// <param name="blueScore">Blue's score (1.0 = win, 0.5 = draw, 0.0 = loss).</param>
    /// <returns>Tuple of new (BlueElo, RedElo).</returns>
    public static (int NewBlueElo, int NewRedElo) Calculate(int blueElo, int redElo, double blueScore)
    {
        // Expected score for Blue
        var expectedBlue = 1.0 / (1.0 + Math.Pow(10, (redElo - blueElo) / 400.0));
        
        // Expected score for Red
        var expectedRed = 1.0 - expectedBlue;
        var redScore = 1.0 - blueScore;
        
        // New ratings
        var newBlueElo = (int)Math.Round(blueElo + KFactor * (blueScore - expectedBlue));
        var newRedElo = (int)Math.Round(redElo + KFactor * (redScore - expectedRed));
        
        // Ensure minimum ELO of 100
        newBlueElo = Math.Max(100, newBlueElo);
        newRedElo = Math.Max(100, newRedElo);
        
        return (newBlueElo, newRedElo);
    }
    
    /// <summary>
    /// Calculates ELO change for a win.
    /// </summary>
    public static (int NewBlueElo, int NewRedElo) ForBlueWin(int blueElo, int redElo) =>
        Calculate(blueElo, redElo, 1.0);
    
    /// <summary>
    /// Calculates ELO change for a loss.
    /// </summary>
    public static (int NewBlueElo, int NewRedElo) ForBlueLoss(int blueElo, int redElo) =>
        Calculate(blueElo, redElo, 0.0);
    
    /// <summary>
    /// Calculates ELO change for a draw.
    /// </summary>
    public static (int NewBlueElo, int NewRedElo) ForDraw(int blueElo, int redElo) =>
        Calculate(blueElo, redElo, 0.5);
}

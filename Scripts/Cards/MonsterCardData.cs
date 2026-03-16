using Godot;

public partial class MonsterCardData : CardData
{
    [Export]
    public int Level { get; set; } = 1;

    [Export]
    public RaceType BonusAgainstRace { get; set; } = RaceType.None;

    [Export]
    public ClassType BonusAgainstClass { get; set; } = ClassType.None;

    [Export]
    public int BonusValue { get; set; } = 0;

    [Export]
    public FleePenaltyType FleePenalty { get; set; } = FleePenaltyType.LoseLevel;

    [Export]
    public int FleeModifier { get; set; } = 0;

    [Export]
    public int Treasures { get; set; } = 1;

    [Export]
    public int LevelsGained { get; set; } = 1;

    public MonsterCardData()
    {
        Type = CardType.Monster;
        DType = DeckType.Dungeon;
    }
}

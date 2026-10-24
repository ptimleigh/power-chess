using PowerChess.Core;

namespace PowerChess.Web.Game;

public static class Names
{
    public static string Name(PowerupKind k) => k switch
    {
        PowerupKind.DoubleMove => "Double Move",
        PowerupKind.Poison => "Poison",
        PowerupKind.SpikedArmor => "Spiked Armor",
        PowerupKind.Stun => "Stun",
        PowerupKind.SmallBomb => "Small Bomb",
        PowerupKind.BigBomb => "Big Bomb",
        PowerupKind.TransformPawn => "Become Pawn",
        PowerupKind.TransformKnight => "Become Knight",
        PowerupKind.TransformBishop => "Become Bishop",
        PowerupKind.TransformRook => "Become Rook",
        PowerupKind.TransformQueen => "Become Queen",
        PowerupKind.Fireball => "Fireball",
        PowerupKind.Steal => "Steal",
        PowerupKind.Boulder => "Boulder",
        PowerupKind.Reroll => "Reroll",
        _ => "",
    };

    public static string Desc(PowerupKind k) => k switch
    {
        PowerupKind.DoubleMove => "This piece must move twice next time it moves. Giving check cancels the second move.",
        PowerupKind.Poison => "If this piece is captured, the capturer dies too.",
        PowerupKind.SpikedArmor => "Uncapturable and immune to effects for 3 turns.",
        PowerupKind.Stun => "Pick any enemy piece. It can't move for 3 turns (kings and armored pieces are immune).",
        PowerupKind.SmallBomb => "Detonate instead of moving: destroys everything in the 3x3 around it (not kings).",
        PowerupKind.BigBomb => "Detonate instead of moving: destroys everything in the 5x5 around it (not kings).",
        PowerupKind.Fireball => "Pick one of your pieces and a direction. It destroys the first enemy in line. A Queen's fireball burns the whole line.",
        PowerupKind.Steal => "Convert an enemy piece that one of your pieces could capture. Your piece stays put.",
        PowerupKind.Boulder => "Place a boulder on an empty square. Nothing can enter or cross it.",
        PowerupKind.Reroll => "Re-draw a powerup you just found, or smash a boulder.",
        _ => PowerupInfo.IsTransform(k) ? "This piece changes type on the spot." : "",
    };

    public static string Icon(PowerupKind k) => k switch
    {
        PowerupKind.DoubleMove => "x2",
        PowerupKind.Poison => "skull",
        PowerupKind.SpikedArmor => "shield",
        PowerupKind.Stun => "star",
        PowerupKind.SmallBomb => "bomb",
        PowerupKind.BigBomb => "bigbomb",
        PowerupKind.Fireball => "fire",
        PowerupKind.Steal => "steal",
        PowerupKind.Boulder => "rock",
        PowerupKind.Reroll => "dice",
        _ => PowerupInfo.IsTransform(k) ? "spark" : "question",
    };

    public static PowerupKind ForBuff(Buff b) => b switch
    {
        Buff.DoubleMove => PowerupKind.DoubleMove,
        Buff.Poison => PowerupKind.Poison,
        Buff.Armor => PowerupKind.SpikedArmor,
        Buff.SmallBomb => PowerupKind.SmallBomb,
        Buff.BigBomb => PowerupKind.BigBomb,
        _ => PowerupKind.None,
    };

    public static readonly PowerupKind[] Guide =
    {
        PowerupKind.DoubleMove, PowerupKind.Poison, PowerupKind.SpikedArmor,
        PowerupKind.SmallBomb, PowerupKind.BigBomb, PowerupKind.TransformQueen, PowerupKind.Fireball,
        PowerupKind.Stun, PowerupKind.Steal, PowerupKind.Boulder, PowerupKind.Reroll,
    };
}

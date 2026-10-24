using PowerChess.Core;

namespace PowerChess.Web.Game;

public static class Names
{
    public static string Name(PowerupKind k) => k switch
    {
        PowerupKind.DoubleMove => "Double Move",
        PowerupKind.Poison => "Poison",
        PowerupKind.SpikedArmor => "Spiked Armor",
        PowerupKind.StunStrike => "Stun Strike",
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
        PowerupKind.StunStrike => "Your next attack with this piece freezes the target for 3 turns instead of capturing.",
        PowerupKind.SmallBomb => "Detonate instead of moving: destroys everything in the 3x3 around it (not kings).",
        PowerupKind.BigBomb => "Detonate instead of moving: destroys everything in the 5x5 around it (not kings).",
        PowerupKind.Fireball => "Pick a piece: it hits the first enemy on each of its attack lines. Knights hit a whole L path.",
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
        PowerupKind.StunStrike => "star",
        PowerupKind.SmallBomb => "bomb",
        PowerupKind.BigBomb => "bigbomb",
        PowerupKind.Fireball => "fire",
        PowerupKind.Steal => "steal",
        PowerupKind.Boulder => "rock",
        PowerupKind.Reroll => "dice",
        _ => PowerupInfo.IsTransform(k) ? "spark" : "question",
    };

    public static readonly PowerupKind[] Guide =
    {
        PowerupKind.DoubleMove, PowerupKind.Poison, PowerupKind.SpikedArmor, PowerupKind.StunStrike,
        PowerupKind.SmallBomb, PowerupKind.BigBomb, PowerupKind.TransformQueen, PowerupKind.Fireball,
        PowerupKind.Steal, PowerupKind.Boulder, PowerupKind.Reroll,
    };
}

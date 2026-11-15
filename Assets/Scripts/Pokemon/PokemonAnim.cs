namespace HavenDuel.Pokemon
{
    /// Logical animation states. Each maps to a clip of the same snake_case name
    /// inside the species' .glb (clips are renamed in the asset prep step).
    public enum PokemonAnim
    {
        Idle, BattleIdle, Walk, Run,
        JumpStart, JumpLoop, FallStart, FallLoop, Land,
        Attack, Attack2, Special, ChargeStart, ChargeLoop, ChargeEnd,
        Hit, Stun, Faint, Roar,
        FlyIdle, FlyBattleIdle, FlyMove, FlyFast, FlyAttack, FlySpecial, FlyHit, FlyLand,
        FlyChargeStart, FlyChargeLoop, FlyChargeEnd,
        FaintLoop,
    }

    public enum Species { Pikachu, Greninja, Charizard, Bulbasaur, Eevee, Mimikyu, Ditto }
}

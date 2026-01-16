public enum PowerupTrigger
{
    // Triggered by the next wall collision while the ball is in-flight.
    NextWallContact = 0,

    // Triggered during landing multiplier evaluation at run end.
    NextLandingEval = 1,

    // Triggered after a missed catch (retroactive arming).
    MissedCatchRetro = 2,

    // Triggered at end-of-run offer stage (Encore is special; still manual).
    EndOfRunOffer = 3,
}
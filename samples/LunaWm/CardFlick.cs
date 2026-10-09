namespace LunaWm;

internal static class CardFlick
{
    public const double CloseVelocity = -1100;
    public const double CloseDistance = -50;
    public const double CloseMinimumVelocity = -500;

    public static FlickOutcome Decide(double travelY, double velocityY, double centerY, double halfHeight, double outputHeight)
    {
        var offscreen = centerY + halfHeight < 0 || centerY - halfHeight > outputHeight || centerY < 0 || centerY > outputHeight;
        if ((velocityY < CloseVelocity && travelY < CloseDistance) || (velocityY < CloseMinimumVelocity && offscreen))
        {
            return FlickOutcome.Close;
        }

        if (velocityY > -CloseMinimumVelocity && travelY > -CloseDistance)
        {
            return FlickOutcome.Maximize;
        }

        return FlickOutcome.SpringBack;
    }
}

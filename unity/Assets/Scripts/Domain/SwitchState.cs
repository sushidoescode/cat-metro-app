using System;

namespace CatMetro.Domain
{
    // Canonical per-switch runtime byte. Route occupies the low two bits; the upper six bits
    // carry cooldown ticks. Pack(route, 0) is byte-identical to every legacy route-only digest.
    public static class SwitchState
    {
        public const int RouteMask = 0x03;
        public const int MaxRoute = RouteMask;
        public const int MaxCooldown = 0x3f;

        public static int Route(byte packed) => packed & RouteMask;

        public static int Cooldown(byte packed) => packed >> 2;

        public static byte Pack(int route, int cooldown)
        {
            if (route < 0 || route > MaxRoute)
                throw new ArgumentOutOfRangeException(nameof(route));
            if (cooldown < 0 || cooldown > MaxCooldown)
                throw new ArgumentOutOfRangeException(nameof(cooldown));
            return (byte)((cooldown << 2) | route);
        }
    }
}

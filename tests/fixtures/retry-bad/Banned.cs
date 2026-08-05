// Negative fixture for CM-C3 criterion 1's [CI] grep (never compiled; grep bait): no shipped
// type under Presentation/** or Application/** may construct the pinned reason — the token
// below is what the gate must fire on. FailReason.PlatformOverflow
class RetryBanned
{
    private object _r; // FailReason.PlatformOverflow
    private object _s; // "Platform overflowed at SRC" — the capital-P literal the grep must catch
}

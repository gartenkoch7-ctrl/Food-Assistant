using FoodAssistant.Domain.Expiry;
using FoodAssistant.Domain.Quantities;

namespace FoodAssistant.Domain.Inventory;

/// <summary>
/// A concrete amount of a product with its own expiry date, storage type and state ("Charge").
/// Expiry dates follow the state table in docs/kb/vorrat-und-haltbarkeit.md.
/// </summary>
/// <remarks>
/// <para>
/// Invalid transitions throw <see cref="InvalidOperationException"/>, invalid arguments throw
/// <see cref="ArgumentException"/>; the application layer offers only valid actions.
/// </para>
/// <para>
/// Expiry kind of a rule-derived date: the KB does not decide it. Whenever a shelf-life rule
/// determines <see cref="ExpiresOn"/>, <see cref="ExpiryKind"/> is <see cref="ExpiryKind.UseBy"/>,
/// because rule texts read "consume within n days" and the "often still fine" hint for best-before
/// dates must not be shown for e.g. thawed meat (AGENTS.md 2.3). The printed label stays in
/// <see cref="PackageExpiryKind"/>.
/// </para>
/// </remarks>
public sealed class StockBatch
{
    // For Restore and the factory methods only; persistence goes through StockBatchSnapshot.
    private StockBatch()
    {
    }

    /// <summary>Creates a sealed batch with the date printed on the package.</summary>
    /// <param name="id">Batch id (UUIDv7, created by the caller).</param>
    /// <param name="quantity">Amount; must be greater than zero.</param>
    /// <param name="storageType">Type of the storage location. Frozen goods bought frozen stay
    /// <see cref="BatchState.Sealed"/> with their package date; <see cref="Freeze"/> is for freezing at home.</param>
    /// <param name="packageExpiresOn">Printed date, or null if there is none.</param>
    /// <param name="packageExpiryKind">Kind of the printed date; <see cref="ExpiryKind.None"/> exactly when there is no date.</param>
    public StockBatch(Guid id, Quantity quantity, StorageType storageType, DateOnly? packageExpiresOn, ExpiryKind packageExpiryKind)
    {
        if (packageExpiresOn.HasValue == (packageExpiryKind == ExpiryKind.None))
        {
            throw new ArgumentException("An expiry kind is required exactly when a package date is given.", nameof(packageExpiryKind));
        }

        Initialize(id, quantity, storageType, BatchState.Sealed);
        PackageExpiresOn = packageExpiresOn;
        PackageExpiryKind = packageExpiryKind;
        ExpiresOn = packageExpiresOn;
        ExpiryKind = packageExpiryKind;
    }

    /// <summary>Unique id of the batch.</summary>
    public Guid Id { get; private set; }

    /// <summary>Remaining amount; zero once used up.</summary>
    public Quantity Quantity { get; private set; }

    /// <summary>Type of the current storage location.</summary>
    public StorageType StorageType { get; private set; }

    /// <summary>Current state.</summary>
    public BatchState State { get; private set; }

    /// <summary>Date printed on the package; kept unchanged ("rests") while frozen.</summary>
    public DateOnly? PackageExpiresOn { get; private set; }

    /// <summary>Kind of the printed date.</summary>
    public ExpiryKind PackageExpiryKind { get; private set; }

    /// <summary>Effective expiry date used for status and FEFO; null means "without date".</summary>
    public DateOnly? ExpiresOn { get; private set; }

    /// <summary>Kind of <see cref="ExpiresOn"/>; see the remarks on rule-derived dates.</summary>
    public ExpiryKind ExpiryKind { get; private set; }

    /// <summary>Day the batch was opened.</summary>
    public DateOnly? OpenedOn { get; private set; }

    /// <summary>Day the batch was frozen at home.</summary>
    public DateOnly? FrozenOn { get; private set; }

    /// <summary>Day the batch was thawed.</summary>
    public DateOnly? ThawedOn { get; private set; }

    /// <summary>Day a leftover or home-made dish was prepared.</summary>
    public DateOnly? PreparedOn { get; private set; }

    /// <summary>True once the whole amount is consumed.</summary>
    public bool IsEmpty => Quantity.IsZero;

    /// <summary>Creates a leftover or home-made batch dated by the rule "as leftover".</summary>
    /// <param name="id">Batch id.</param>
    /// <param name="quantity">Amount; must be greater than zero.</param>
    /// <param name="storageType">Where the leftover is stored; pick the rule for this type.</param>
    /// <param name="today">Day of preparation.</param>
    /// <param name="rule">Resolved rule; without "as leftover" the batch has no date.</param>
    public static StockBatch CreateLeftover(
        Guid id,
        Quantity quantity,
        StorageType storageType,
        DateOnly today,
        ShelfLifeRule rule)
    {
        ArgumentNullException.ThrowIfNull(rule);
        var batch = new StockBatch { PackageExpiryKind = ExpiryKind.None, PreparedOn = today };
        batch.Initialize(id, quantity, storageType, BatchState.Leftover);
        batch.SetRuleDate(today, rule.DaysAsLeftover);
        return batch;
    }

    /// <summary>Rebuilds a batch from persisted state; counterpart of <see cref="StockBatchSnapshot.From"/>.</summary>
    /// <exception cref="ArgumentException">The snapshot is inconsistent (<see cref="StockBatchSnapshot.Validate"/>).</exception>
    public static StockBatch Restore(StockBatchSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        snapshot.Validate();
        return new StockBatch
        {
            Id = snapshot.Id,
            Quantity = snapshot.Quantity,
            StorageType = snapshot.StorageType,
            State = snapshot.State,
            PackageExpiresOn = snapshot.PackageExpiresOn,
            PackageExpiryKind = snapshot.PackageExpiryKind,
            ExpiresOn = snapshot.ExpiresOn,
            ExpiryKind = snapshot.ExpiryKind,
            OpenedOn = snapshot.OpenedOn,
            FrozenOn = snapshot.FrozenOn,
            ThawedOn = snapshot.ThawedOn,
            PreparedOn = snapshot.PreparedOn,
        };
    }

    /// <summary>
    /// Marks a sealed batch as opened: effective date = min(package date, today + "after opening").
    /// Without the rule value the package date stays (no computed date).
    /// </summary>
    /// <exception cref="InvalidOperationException">The batch is not sealed.</exception>
    public void Open(DateOnly today, ShelfLifeRule rule)
    {
        ArgumentNullException.ThrowIfNull(rule);
        EnsureState(State == BatchState.Sealed, "Only a sealed batch can be opened.");
        EnsureNotBeforeLastChange(today);

        State = BatchState.Opened;
        OpenedOn = today;
        (ExpiresOn, ExpiryKind) = ShelfLifeDates.AfterOpening(today, rule.DaysAfterOpening, PackageExpiresOn, PackageExpiryKind);
    }

    /// <summary>
    /// Freezes the batch at home: effective date = today + "frozen"; the package date rests
    /// (stays stored, but no longer counts). Without the rule value the batch has no date.
    /// </summary>
    /// <remarks>
    /// Thawed food cannot be frozen again: the KB does not decide this, refreezing is a known food
    /// safety risk, so the domain refuses it (conservative). A cooked dish is recorded as a leftover.
    /// </remarks>
    /// <exception cref="InvalidOperationException">The batch is already frozen or was thawed.</exception>
    public void Freeze(DateOnly today, ShelfLifeRule rule)
    {
        ArgumentNullException.ThrowIfNull(rule);
        EnsureState(StorageType != StorageType.Frozen, "The batch is already in the freezer.");
        EnsureState(State != BatchState.Thawed, "Thawed food must not be frozen again.");
        EnsureNotBeforeLastChange(today);

        State = BatchState.Frozen;
        StorageType = StorageType.Frozen;
        FrozenOn = today;
        SetRuleDate(today, rule.DaysFrozen);
    }

    /// <summary>
    /// Thaws a batch from the freezer: effective date = today + "after thawing". Without the rule
    /// value the batch has no date. Applies to food frozen at home and to frozen goods bought frozen.
    /// </summary>
    /// <param name="today">Day of thawing.</param>
    /// <param name="rule">Resolved rule for the target storage type.</param>
    /// <param name="targetStorage">Where the batch goes; must not be <see cref="StorageType.Frozen"/>.</param>
    /// <exception cref="InvalidOperationException">The batch is not in the freezer.</exception>
    public void Thaw(DateOnly today, ShelfLifeRule rule, StorageType targetStorage = StorageType.Chilled)
    {
        ArgumentNullException.ThrowIfNull(rule);
        if (targetStorage == StorageType.Frozen)
        {
            throw new ArgumentException("Thawing moves the batch out of the freezer.", nameof(targetStorage));
        }

        EnsureState(StorageType == StorageType.Frozen, "Only a batch in the freezer can be thawed.");
        EnsureNotBeforeLastChange(today);

        State = BatchState.Thawed;
        StorageType = targetStorage;
        ThawedOn = today;
        SetRuleDate(today, rule.DaysAfterThawing);
    }

    /// <summary>Consumes part or all of the batch; dates and state stay unchanged.</summary>
    /// <param name="amount">Amount to take; must be greater than zero and convertible.</param>
    /// <exception cref="InvalidOperationException">
    /// The unit does not convert, or <paramref name="amount"/> exceeds <see cref="Quantity"/>.
    /// </exception>
    public void Consume(Quantity amount)
    {
        if (amount.Unit is null || amount.IsZero)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), "The amount must be greater than zero.");
        }

        Quantity = Quantity.Subtract(amount);
    }

    private void Initialize(Guid id, Quantity quantity, StorageType storageType, BatchState state)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("The id must not be empty.", nameof(id));
        }

        if (quantity.Unit is null || quantity.IsZero)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity), "A new batch needs an amount greater than zero.");
        }

        Id = id;
        Quantity = quantity;
        StorageType = storageType;
        State = state;
    }

    private void SetRuleDate(DateOnly from, int? days) => (ExpiresOn, ExpiryKind) = ShelfLifeDates.FromRule(from, days);

    private void EnsureNotBeforeLastChange(DateOnly today)
    {
        DateOnly?[] changes = [OpenedOn, FrozenOn, ThawedOn, PreparedOn];
        if (changes.Any(change => change > today))
        {
            throw new ArgumentOutOfRangeException(nameof(today), "A state change cannot predate the previous one.");
        }
    }

    private static void EnsureState(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}

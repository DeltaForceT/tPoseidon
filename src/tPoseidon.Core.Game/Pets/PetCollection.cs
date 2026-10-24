using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Game.Pets;

public enum PetCollectionState : byte
{
    Idle = 0,
    Collecting = 1,
    Stopped = 2,
    Returned = 3,
}

public sealed record PetCollectionSlot(ItemParamEntry? Definition, bool Equipped, bool Expired, int Stack);

public sealed record PetCollectionStartRequest(
    PetCollectionState State,
    int CharacterLevel,
    bool Fainted,
    PetCollectionSlot? Pet,
    PetCollectionSlot? Food,
    int FoodCount,
    int MapId,
    int BagCount);

public sealed record PetCollectionPlan(int PetItemId, int PetLevel, PetDigPetEntry Pet, PetDigFoodEntry Food, PetDigExEntry? ExRow, bool PetDigsMines, bool FoodDigsMines)
{
    public TimeSpan TickPeriod => TimeSpan.FromSeconds(Food.SpeedSeconds);
}

public readonly record struct PetCollectionDraw(int ItemId, int Count, bool IsExItem);

public static class PetCollectionRules
{
    public const byte Ok = 0;
    public const byte Unknown = 1;
    public const byte NotRegistrable = 2;
    public const byte NoPet = 3;
    public const byte BagFull = 4;
    public const byte Equipped = 5;
    public const byte CharacterTooLow = 6;
    public const byte PetAboveCharacter = 7;
    public const byte Expired = 8;
    public const byte NotCollector = 9;
    public const byte AreaClosed = 10;
    public const byte MineNeedsSpecialPet = 11;
    public const byte AreaLevel = 12;
    public const byte MineNeedsSpecialFood = 14;
    public const byte NotFood = 15;
    public const byte StopRefused = 16;
    public const byte ReturnRefused = 17;
    public const byte GetFailed = 18;
    public const byte GetNoRoom = 19;
    public const byte DestroyRefused = 20;

    public const int MinimumCharacterLevel = 30;
    public const int RateScale = 10000;
    public const int NothingDrawn = 0;
    public const int DepthStep = 10;

    public static bool CanStartFrom(PetCollectionState state) => state is PetCollectionState.Idle or PetCollectionState.Stopped;

    public static bool CanStopFrom(PetCollectionState state) => state == PetCollectionState.Collecting;

    public static bool CanReturnFrom(PetCollectionState state) => state is PetCollectionState.Collecting or PetCollectionState.Stopped;

    public static PetCollectionState AfterReturn(int bagCount) => bagCount > 0 ? PetCollectionState.Returned : PetCollectionState.Idle;

    public static PetCollectionState AfterBagChange(PetCollectionState state, int bagCount) =>
        state == PetCollectionState.Returned && bagCount == 0 ? PetCollectionState.Idle : state;

    public static PetCollectionState AtLogin => PetCollectionState.Idle;

    public static byte CheckStart(PetCollectionStartRequest request, PetDigTables tables, out PetCollectionPlan? plan)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(tables);
        plan = null;
        if (!CanStartFrom(request.State)) return NotRegistrable;
        if (request.Fainted) return Unknown;
        if (request.CharacterLevel < MinimumCharacterLevel) return CharacterTooLow;
        if (request.Pet is not { Definition: { } pet } petSlot) return NoPet;
        if (petSlot.Equipped) return Equipped;
        if (petSlot.Expired) return Expired;
        if (!PetRules.IsPet(pet) || !tables.Pets.TryGetValue(pet.RefineIndex, out var collector)) return NotCollector;
        var petLevel = PetLevel(pet);
        if (petLevel > request.CharacterLevel) return PetAboveCharacter;
        if (request.Food is not { Definition: { } food } foodSlot) return NotFood;
        if (foodSlot.Equipped) return Equipped;
        if (foodSlot.Expired) return Expired;
        if (!tables.Foods.TryGetValue(food.Id, out var feed)) return NotFood;
        if (request.FoodCount < 1 || request.FoodCount > foodSlot.Stack || request.FoodCount > feed.MaxItems) return Unknown;
        var candidate = new PetCollectionPlan(pet.Id, petLevel, collector, feed, tables.ExItems.GetValueOrDefault(pet.Id),
            DigsMines(pet), DigsMines(food));
        var areaCode = CheckArea(tables, request.MapId, candidate);
        if (areaCode != Ok) return areaCode;
        if (IsFull(request.BagCount, collector)) return BagFull;
        plan = candidate;
        return Ok;
    }

    public static byte CheckArea(PetDigTables tables, int mapId, PetCollectionPlan plan)
    {
        ArgumentNullException.ThrowIfNull(tables);
        ArgumentNullException.ThrowIfNull(plan);
        if (!tables.Zones.TryGetValue(mapId, out var zone) || !tables.Areas.TryGetValue(zone.Region, out var area)) return AreaClosed;
        if (plan.PetLevel < area.MinLevel || plan.PetLevel > area.MaxLevel) return AreaLevel;
        if (zone.IsMine && !plan.PetDigsMines) return MineNeedsSpecialPet;
        if (zone.IsMine && !plan.FoodDigsMines) return MineNeedsSpecialFood;
        return Ok;
    }

    public static bool DigsMines(ItemParamEntry item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return item.SubType == PetDigTables.SpecialDigSubType;
    }

    public static int DepthPenalty(PetDigTables tables, int mapId, Func<int, int> next)
    {
        ArgumentNullException.ThrowIfNull(tables);
        ArgumentNullException.ThrowIfNull(next);
        if (!tables.Zones.TryGetValue(mapId, out var zone) || zone.MaxItemDepth < 1) return 0;
        var lowest = zone.MinItemDepth / DepthStep;
        var depth = DepthStep * (lowest + next(zone.MaxItemDepth / DepthStep - lowest + 1));
        foreach (var band in tables.DepthBands)
        {
            if (depth >= band.MinDepth && depth <= band.MaxDepth) return band.Variable;
        }
        return 0;
    }

    public static int PetLevel(ItemParamEntry pet)
    {
        ArgumentNullException.ThrowIfNull(pet);
        return pet.MinLevel;
    }

    public static bool IsFull(int bagCount, PetDigPetEntry pet)
    {
        ArgumentNullException.ThrowIfNull(pet);
        return bagCount >= pet.BagSize;
    }

    public static bool StopsAfterTick(int foodLeft, int bagCount, PetDigPetEntry pet) => foodLeft < 1 || IsFull(bagCount, pet);

    public static PetCollectionDraw? Draw(PetCollectionPlan plan, int depthPenalty, Func<int, int> next, Func<(int ItemId, int Count)> groundItem)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(next);
        ArgumentNullException.ThrowIfNull(groundItem);
        if (next(RateScale) >= plan.Pet.DigRate - depthPenalty) return null;
        if (plan.ExRow is { Items.Count: > 0 } ex && next(RateScale) < ex.ExRate + plan.Food.PlusRate)
        {
            var total = ex.Items.Sum(item => item.Rate);
            var roll = next(total);
            var floor = 0;
            foreach (var item in ex.Items)
            {
                floor += item.Rate;
                if (roll < floor) return new PetCollectionDraw(item.ItemId, item.Count, true);
            }
        }
        var (itemId, count) = groundItem();
        return itemId == NothingDrawn || count < 1 ? null : new PetCollectionDraw(itemId, count, false);
    }
}

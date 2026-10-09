using System;
using System.Collections.Generic;
using System.IO;

namespace Cogworks.Sim
{
    /// <summary>
    /// Crafts its selected recipe while its kinetic network turns; speed scales with RPM (32 rpm = recipe
    /// time). Ingredients are consumed when a craft starts and refunded if it's removed mid-craft.
    /// </summary>
    public sealed class AssemblerLogic : BuildingLogic
    {
        const int BaseRpm = 32;
        const int OutputCap = 100;
        // Inserters keep this many crafts' worth of each ingredient buffered.
        const int InserterBatches = 2;

        public readonly Inventory Inputs = new Inventory(6);
        public ItemStack Output;
        public ushort RecipeId { get; private set; }
        public bool Crafting { get; private set; }
        public int Progress { get; private set; }
        public int LastRpm { get; private set; }

        public Recipe Recipe => RecipeCatalog.Get(RecipeId);
        public int WorkNeeded => (Recipe?.Ticks ?? 1) * BaseRpm;

        public override void Tick(World world)
        {
            var recipe = Recipe;
            LastRpm = world.Kinetics.RpmAt(Building);
            if (recipe == null || LastRpm <= 0) return;

            if (!Crafting)
            {
                if (!HasIngredients(recipe) || !OutputHasRoom(recipe)) return;
                foreach (var input in recipe.Inputs) Inputs.Remove(input.Item, input.Count);
                Crafting = true;
                Progress = 0;
            }

            Progress += LastRpm;
            if (Progress < WorkNeeded) return;
            Output = new ItemStack(recipe.Output.Item, Output.Count + recipe.Output.Count);
            Crafting = false;
            Progress = 0;
        }

        bool HasIngredients(Recipe recipe)
        {
            foreach (var input in recipe.Inputs)
                if (Inputs.Count(input.Item) < input.Count) return false;
            return true;
        }

        bool OutputHasRoom(Recipe recipe) =>
            Output.IsEmpty || (Output.Item == recipe.Output.Item && Output.Count + recipe.Output.Count <= OutputCap);

        /// <summary>Switches recipe, moving everything inside into <paramref name="refund"/>.</summary>
        public void SetRecipe(ushort recipeId, List<ItemStack> refund)
        {
            if (recipeId == RecipeId) return;
            CollectContents(refund);
            Inputs.Clear();
            Output = default;
            Crafting = false;
            Progress = 0;
            RecipeId = recipeId;
        }

        public override bool CanAccept(ushort item, Int2 fromTile)
        {
            int need = Recipe?.InputCount(item) ?? 0;
            return need > 0 && Inputs.Count(item) < need * InserterBatches && Inputs.CanInsert(item);
        }

        public override void Insert(ushort item, Int2 fromTile) => Inputs.Insert(item, 1);

        public override int InsertFromPlayer(ushort item, int count)
        {
            if ((Recipe?.InputCount(item) ?? 0) == 0) return 0;
            return Inputs.Insert(item, count);
        }

        public override bool TryTake(Func<ushort, bool> accept, out ushort item)
        {
            item = Output.Item;
            if (Output.IsEmpty || (accept != null && !accept(item))) return false;
            Output.Count--;
            if (Output.Count <= 0) Output = default;
            return true;
        }

        public override int TakeToPlayer(ushort item, int count)
        {
            int moved = 0;
            if (!Output.IsEmpty && Output.Item == item)
            {
                moved = Math.Min(count, Output.Count);
                Output.Count -= moved;
                if (Output.Count <= 0) Output = default;
            }
            return moved + Inputs.Remove(item, count - moved);
        }

        public override void CollectContents(List<ItemStack> into)
        {
            AddContents(into, Inputs);
            AddContents(into, Output.Item, Output.Count);
            if (Crafting && Recipe != null) into.AddRange(Recipe.Inputs);
        }

        internal override void Write(BinaryWriter w)
        {
            w.Write(RecipeId);
            Inputs.Write(w);
            FurnaceLogic.WriteStack(w, Output);
            w.Write(Crafting);
            w.Write(Progress);
        }

        internal override void Read(BinaryReader r)
        {
            RecipeId = r.ReadUInt16();
            Inputs.Read(r);
            Output = FurnaceLogic.ReadStack(r);
            Crafting = r.ReadBoolean();
            Progress = r.ReadInt32();
        }

        internal override void AddToHash(ref Fnv64 h)
        {
            h.Add(RecipeId);
            Inputs.AddToHash(ref h);
            h.Add(Output.Item);
            h.Add(Output.Count);
            h.Add(Crafting ? 1 : 0);
            h.Add(Progress);
        }
    }
}

using PlushieOdyssey.Combat.Views;
namespace PlushieOdyssey.Combat
{
    public interface IHaveCaster
    {
        public CombatantView Caster { get; }
    }
}
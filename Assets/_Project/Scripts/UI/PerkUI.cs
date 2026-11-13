using PlushieOdyssey.Combat.Models;
using UnityEngine;
using UnityEngine.UI;

namespace PlushieOdyssey.UI
{
    public class PerkUI : MonoBehaviour
    {
        public Perk Perk { get; private set; }

        [SerializeField] private Image image;

        public void Setup(Perk perk)
        {
            Perk = perk;
            image.sprite = perk.Image;
        }
    }
}

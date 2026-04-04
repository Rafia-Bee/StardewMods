using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.GameData.Objects;
using StardewValley.GameData.Shops;

namespace LivestockFollowsYou.Framework;

/// <summary>Registers the Grazing Bell item and injects it into Data/Objects and Marnie's shop.</summary>
internal class GrazingBellItem
{
    public const string ItemId = "RafiaBee.LivestockFollowsYou_GrazingBell";
    public const string QualifiedItemId = "(O)" + ItemId;
    private const string TexturePath = "Mods/RafiaBee.LivestockFollowsYou/GrazingBell";

    private readonly IModHelper Helper;

    public GrazingBellItem(IModHelper helper)
    {
        Helper = helper;
    }

    public void Register()
    {
        Helper.Events.Content.AssetRequested += OnAssetRequested;
    }

    private void OnAssetRequested(object sender, AssetRequestedEventArgs e)
    {
        if (e.NameWithoutLocale.IsEquivalentTo("Data/Objects"))
        {
            e.Edit(asset =>
            {
                var data = asset.AsDictionary<string, ObjectData>().Data;
                data[ItemId] = new ObjectData
                {
                    Name = ItemId,
                    DisplayName = Helper.Translation.Get("item.grazing_bell.name"),
                    Description = Helper.Translation.Get("item.grazing_bell.description"),
                    Type = "Basic",
                    Category = -999,
                    Price = 500,
                    Texture = TexturePath,
                    SpriteIndex = 0,
                    CanBeGivenAsGift = false,
                    ExcludeFromShippingCollection = true
                };
            });
        }
        else if (e.NameWithoutLocale.IsEquivalentTo("Data/Shops"))
        {
            e.Edit(asset =>
            {
                var data = asset.AsDictionary<string, ShopData>().Data;
                if (data.TryGetValue("AnimalShop", out var shop))
                {
                    shop.Items.Add(new ShopItemData
                    {
                        Id = ItemId,
                        ItemId = QualifiedItemId,
                        Price = 500,
                        Condition = null
                    });
                }
            });
        }
        else if (e.NameWithoutLocale.IsEquivalentTo(TexturePath))
        {
            e.LoadFromModFile<Texture2D>("assets/GrazingBell.png", AssetLoadPriority.Exclusive);
        }
    }
}

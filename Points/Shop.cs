using Life.Network;
using Life.UI;
using SQLite;
using System.Threading.Tasks;
using ModKit.Helper;
using ModKit.Helper.PointHelper;
using mk = ModKit.Helper.TextFormattingHelper;
using System.Collections.Generic;
using System.Linq;
using Life;
using PointShop.Entities;
using ModKit.Utils;
using Newtonsoft.Json;
using System;

namespace PointShop.Points
{
    public class Shop : ModKit.ORM.ModEntity<Shop>, PatternData
    {
        [AutoIncrement][PrimaryKey] public int Id { get; set; }
        public string TypeName { get; set; }
        public string PatternName { get; set; }

        //Declare your other properties here
        public string Items { get; set; }
        [Ignore] public List<int> LItems { get; set; }
        public string BizAllowed { get; set; }
        [Ignore] public List<int> LBizAllowed { get; set; }

        [Ignore] public ModKit.ModKit Context { get; set; }

        public Shop() { }
        public Shop(bool isCreated)
        {
            TypeName = nameof(Shop);
        }

        /// <summary>
        /// Applies the properties retrieved from the database during the generation of a point in the game using this model.
        /// </summary>
        /// <param name="patternId">The identifier of the pattern in the database.</param>
        public async Task SetProperties(int patternId)
        {
            Console.WriteLine(patternId);
            var result = await Query(patternId);

            Id = patternId;
            TypeName = nameof(Shop);
            PatternName = result.PatternName;

            //Add your other properties here
            Items = result.Items;
            LItems = ListConverter.ReadJson(Items);
            BizAllowed = result.BizAllowed;
            LBizAllowed = ListConverter.ReadJson(BizAllowed);
        }

        /// <summary>
        /// Contains the action to perform when a player interacts with the point.
        /// </summary>
        /// <param name="player">The player interacting with the point.</param>
        public void OnPlayerTrigger(Player player)
        {
            if (LBizAllowed.Count == 0 || (player.HasBiz() && LBizAllowed.Contains(player.biz.Id))) PointShopPanel(player);
            else player.Notify("PointShop", "Vous n'avez pas la permission d'accéder à cette boutique.", NotificationManager.Type.Info);
        }

        #region CUSTOM
        public async void PointShopPanel(Player player)
        {
            var query = await PointShop_Item.QueryAll();
            List<PointShop_Item> items = query.Where(i => LItems.Contains(i.Id)).ToList();

            Panel panel = Context.PanelHelper.Create($"{PatternName}", UIPanel.PanelType.TabPrice, player, () => PointShopPanel(player));

            foreach (var item in items)
            {
                var currentItem = ItemUtils.GetItemById(item.ItemId);
                panel.AddTabLine($"{currentItem.itemName}", $"{item.Price}€", ItemUtils.GetIconIdByItemId(item.ItemId), _ => {});
            }

            if(items.Count > 0)
            {
                panel.NextButton("Acheter", () =>
                {
                    if (items[panel.selectedTab].IsBuyable) PointShopBuyPanel(player, items[panel.selectedTab]);
                    else
                    {
                        player.Notify("PointShop", "Cette objet n'est pas achetable", NotificationManager.Type.Info);
                        panel.Refresh();
                    }
                });
                panel.NextButton("Vendre", () =>
                {
                    var currentItem = ItemUtils.GetItemById(items[panel.selectedTab].ItemId);
                    if (items[panel.selectedTab].IsResellable) PointShopSellPanel(player, items[panel.selectedTab]);
                    else
                    {
                        player.Notify("PointShop", "Cette objet n'est pas vendable", NotificationManager.Type.Info);
                        panel.Refresh();
                    }
                });
            }


            if(LBizAllowed.Count > 0)
            {
                panel.NextButton("Historique", async () =>
                {
                    if (player.HasBiz())
                    {
                        var permissions = await PermissionUtils.GetPlayerPermission(player);
                        if (player.biz.OwnerId == player.character.Id || (permissions.hasRemoveMoneyPermission && permissions.hasAddMoneyPermission)) PointShopLogsPanel(player);
                        else player.Notify("PointShop", "Vous ne disposez pas des droits sur le compte bancaire d'entreprise", Life.NotificationManager.Type.Warning);
                    }
                    else player.Notify("PointShop", "Vous devez être propriétaire ou avoir les droits sur le compte en banque de votre société", Life.NotificationManager.Type.Warning);
                });
            }
            if(player.IsAdmin && player.serviceAdmin) panel.NextButton("Admin", () => PointShopAdminPanel(player));
            panel.CloseButton();

            panel.Display();
        }
        public async void PointShopLogsPanel(Player player)
        {
            var query = await PointShop_Logs.QueryAll();
            List<PointShop_Logs> logs = query.Where(l => l.ShopId == Id && l.BizId == player.character.BizId).ToList();
            logs.Reverse();

            Panel panel = Context.PanelHelper.Create($"{PatternName} - Historique", UIPanel.PanelType.TabPrice, player, () => PointShopLogsPanel(player));

            foreach (var log in logs)
            {
                var currentItem = ItemUtils.GetItemById(log.ItemId);
                panel.AddTabLine($"{mk.Color($"{(log.IsPurchase ? "ACHAT" : "VENTE")}", (log.IsPurchase ? mk.Colors.Success : mk.Colors.Orange))} par {mk.Color(log.CharacterFullName, mk.Colors.Info)}<br>" +
                    $"{mk.Size($"{currentItem.itemName} x {mk.Color($"{log.Quantity}", mk.Colors.Warning)}", 14)}",
                    $"{DateUtils.ConvertNumericalDateToString(log.CreatedAt)}<br>{mk.Align($"{mk.Color($"{(log.IsPurchase ? "-" : "+")} {log.Price * log.Quantity}€", mk.Colors.Verbose)}", mk.Aligns.Center)}",
                    ItemUtils.GetIconIdByItemId(currentItem.id), _ => {});
            }

            panel.PreviousButton();
            panel.CloseButton();

            panel.Display();
        }
        public async void PointShopAdminPanel(Player player)
        {
            var query = await PointShop_Item.QueryAll();
            List<PointShop_Item> items = query.Where(i => LItems.Contains(i.Id)).ToList();

            Panel panel = Context.PanelHelper.Create($"{PatternName} - Modifier la boutique", UIPanel.PanelType.TabPrice, player, () => PointShopAdminPanel(player));

            foreach (var item in items)
            {
                var currentItem = ItemUtils.GetItemById(item.ItemId);
                panel.AddTabLine($"{currentItem.itemName}", $"{item.Price}€", ItemUtils.GetIconIdByItemId(item.ItemId), _ => PointShopAdminItemPanel(player, item));
            }

            panel.NextButton("Ajouter", () => PointShopAddItemPanel(player));
            if(items.Count > 0) panel.AddButton("Modifier", _ => panel.SelectTab());
            panel.PreviousButton();
            panel.CloseButton();

            panel.Display();
        }
        public void PointShopAddItemPanel(Player player)
        {
            Panel panel = Context.PanelHelper.Create($"{PatternName} - Ajouter un article", UIPanel.PanelType.Input, player, () => PointShopAddItemPanel(player));

            panel.TextLines.Add("Renseigner l'ID");
            panel.inputPlaceholder = "exemple: 5";

            panel.PreviousButtonWithAction("Confirmer", async () =>
            {
                if (int.TryParse(panel.inputText, out int itemId))
                {
                    if (ItemUtils.GetItemById(itemId) != null)
                    {
                        PointShop_Item newItem = new PointShop_Item();
                        newItem.ItemId = itemId;
                        newItem.Price = 1;
                        newItem.IsBuyable = true;
                        newItem.IsResellable = true;
                        if (await newItem.Save())
                        {
                            LItems.Add(newItem.Id);
                            Items= ListConverter.WriteJson(LItems);
                            await Save();
                            player.Notify("PointShop", $"Article enregistré", NotificationManager.Type.Success);
                            return true;
                        }
                        else
                        {
                            player.Notify("PointShop", $"Nous n'avons pas pu enregistrer cette article", NotificationManager.Type.Error);
                            return false;
                        }
                    }
                    else
                    {
                        player.Notify("PointShop", $"Aucun objet ne correspond à l'ID {itemId}", NotificationManager.Type.Warning);
                        return false;
                    }
                }
                else
                {
                    player.Notify("PointShop", "Format incorrect", NotificationManager.Type.Warning);
                    return false;
                }
            });

            panel.PreviousButton();
            panel.CloseButton();

            panel.Display();
        }
        public void PointShopBuyPanel(Player player, PointShop_Item item)
        {
            Panel panel = Context.PanelHelper.Create($"{PatternName} - Achat", UIPanel.PanelType.Input, player, () => PointShopBuyPanel(player, item));

            panel.TextLines.Add("Renseigner la quantité");

            panel.PreviousButtonWithAction("Confirmer", async () => {
                if(int.TryParse(panel.inputText, out int quantity))
                {
                    if(quantity > 0)
                    {
                        double total = quantity * item.Price;
                        if (player.character.Money >= total)
                        {
                            if (player.setup.inventory.AddItem(item.ItemId, quantity, null))
                            {
                                player.AddMoney(-total, $"PointShop - {PatternName}");
                                var currentItem = ItemUtils.GetItemById(item.ItemId);
                                player.Notify("Achat", $"Vous venez d'acheter {quantity} {currentItem.itemName} pour {quantity * item.Price}€", NotificationManager.Type.Success);

                                PointShop_Logs newLog = new PointShop_Logs();
                                newLog.ShopId = Id;
                                newLog.CharacterId = player.character.Id;
                                newLog.CharacterFullName = player.GetFullName();
                                newLog.ItemId = item.ItemId;
                                newLog.BizId = player.character.BizId;
                                newLog.Quantity = quantity;
                                newLog.IsPurchase = true;
                                newLog.Price = item.Price;
                                newLog.CreatedAt = DateUtils.GetNumericalDateOfTheDay();
                                await newLog.Save();

                                return true;
                            }
                            else
                            {
                                player.Notify("Achat", $"Vous n'avez pas suffisament d'espace dans votre inventaire", NotificationManager.Type.Warning);
                                return false;
                            }
                        }
                        else
                        {
                            player.Notify("Achat", $"Vous n'avez pas suffisament d'argent ({total}€)", NotificationManager.Type.Warning);
                            return false;
                        }
                    }
                    else
                    {
                        player.Notify("Achat", $"Quantité incorrect", NotificationManager.Type.Warning);
                        return false;
                    }
                }
                else
                {
                    player.Notify("Achat", $"Format incorrect", NotificationManager.Type.Warning);
                    return false;
                }
            });
            panel.CloseButton();

            panel.Display();
        }
        public void PointShopSellPanel(Player player, PointShop_Item item)
        {
            Panel panel = Context.PanelHelper.Create($"{PatternName} - Vendre", UIPanel.PanelType.Input, player, () => PointShopSellPanel(player, item));

            panel.TextLines.Add("Renseigner la quantité");

            panel.PreviousButtonWithAction("Confirmer", async () => {
                if (int.TryParse(panel.inputText, out int quantity))
                {
                    if (quantity > 0)
                    {
                        double total = quantity * item.Price;
                       
                        if (InventoryUtils.CheckInventoryContainsItem(player, item.ItemId, quantity))
                        {
                            InventoryUtils.RemoveFromInventory(player, item.ItemId, quantity);
                            player.AddMoney(total, $"PointShop - {PatternName}");
                            var currentItem = ItemUtils.GetItemById(item.ItemId);
                            player.Notify("Achat", $"Vous venez de vendre {quantity} {currentItem.itemName} pour {quantity * item.Price}€", NotificationManager.Type.Success);

                            PointShop_Logs newLog = new PointShop_Logs();
                            newLog.ShopId = Id;
                            newLog.CharacterId = player.character.Id;
                            newLog.CharacterFullName = player.GetFullName();
                            newLog.ItemId = item.ItemId;
                            newLog.BizId = player.character.BizId;
                            newLog.Quantity = quantity;
                            newLog.IsPurchase = false;
                            newLog.Price = item.Price;
                            newLog.CreatedAt = DateUtils.GetNumericalDateOfTheDay();
                            await newLog.Save();

                            return true;
                        }
                        else
                        {
                            var currentItem = ItemUtils.GetItemById(item.ItemId);
                            player.Notify("Achat", $"Vous n'avez pas suffisament de {currentItem.itemName} dans votre inventaire", NotificationManager.Type.Warning);
                            return false;
                        }
                        
                        
                    }
                    else
                    {
                        player.Notify("Achat", $"Quantité incorrect", NotificationManager.Type.Warning);
                        return false;
                    }
                }
                else
                {
                    player.Notify("Achat", $"Format incorrect", NotificationManager.Type.Warning);
                    return false;
                }
            });
            panel.CloseButton();

            panel.Display();
        }     
        public void PointShopAdminItemPanel(Player player, PointShop_Item item)
        {
            var currentItem = ItemUtils.GetItemById(item.ItemId);
            Panel panel = Context.PanelHelper.Create($"{PatternName} - Modifier un article", UIPanel.PanelType.TabPrice, player, () => PointShopAdminItemPanel(player, item));

            panel.AddTabLine($"{mk.Color("Objet:", mk.Colors.Info)} {currentItem.itemName}", "", ItemUtils.GetIconIdByItemId(item.ItemId), _ =>
            {
                player.Notify("PointShop", "Vous ne pouvez pas modifier cette valeur", NotificationManager.Type.Warning);
                panel.Refresh();
            });
            panel.AddTabLine($"{mk.Color("Prix:", mk.Colors.Info)} {item.Price}€", "", ItemUtils.GetIconIdByItemId(item.ItemId), _ =>
            {
                PointShopItemPricePanel(player, item);
            });
            panel.AddTabLine($"{mk.Color("Achetable:", mk.Colors.Info)} {(item.IsBuyable ? "Oui" : "Non")}", "", ItemUtils.GetIconIdByItemId(item.ItemId), async _ =>
            {
                item.IsBuyable = !item.IsBuyable;
                if (await item.Save()) player.Notify("PointShop", "Modification enregistrée", NotificationManager.Type.Success);
                else player.Notify("PointShop", "Nous n'avons pas pu enregistrer cette modification", NotificationManager.Type.Error);
                panel.Refresh();
            });
            panel.AddTabLine($"{mk.Color("Vendable:", mk.Colors.Info)} {(item.IsResellable ? "Oui" : "Non")}", "", ItemUtils.GetIconIdByItemId(item.ItemId), async _ =>
            {
                item.IsResellable = !item.IsResellable;
                if (await item.Save()) player.Notify("PointShop", "Modification enregistrée", NotificationManager.Type.Success);
                else player.Notify("PointShop", "Nous n'avons pas pu enregistrer cette modification", NotificationManager.Type.Error);
                panel.Refresh();
            });

            panel.NextButton("Sélectionner", () => panel.SelectTab());
            panel.PreviousButtonWithAction("Supprimer", async () =>
            {
                LItems.Remove(item.Id);
                Items = JsonConvert.SerializeObject(LItems);
                if(await Save())
                {
                    player.Notify("PointShop", "Suppression confirmée", NotificationManager.Type.Success);
                    return true;
                }else
                {
                    player.Notify("PointShop", "Nous n'avons pas pu enregistrer cette suppression", NotificationManager.Type.Error);
                    return false;
                }
                
            });
            panel.PreviousButton();
            panel.CloseButton();

            panel.Display();
        }
        public void PointShopItemPricePanel(Player player, PointShop_Item item)
        {
            Panel panel = Context.PanelHelper.Create($"{PatternName} - Modifier le prix", UIPanel.PanelType.Input, player, () => PointShopItemPricePanel(player, item));

            panel.TextLines.Add("Définir le prix");
            panel.inputPlaceholder = "exemple: 1.50";

            panel.PreviousButtonWithAction("Confirmer", async () =>
            {
                string inputText = panel.inputText.Replace(",", ".");
                if (double.TryParse(inputText, out double price))
                {
                    item.Price = Math.Round(price, 2);
                    if(await item.Save())
                    {
                        player.Notify("PointShop", "Modification enregistrée", NotificationManager.Type.Success);
                        return true;
                    }else
                    {
                        player.Notify("PointShop", "Nous n'avons pas pu enregistrer cette modification", NotificationManager.Type.Error);
                        return true;
                    }
                }
                else
                {
                    player.Notify("PointShop", "Format incorrect", NotificationManager.Type.Warning);
                    return false;
                }
            });
            panel.PreviousButton();
            panel.CloseButton();

            panel.Display();
        }   
        #endregion

        /// <summary>
        /// Triggers the function to begin creating a new model.
        /// </summary>
        /// <param name="player">The player initiating the creation of the new model.</param>
        public void SetPatternData(Player player)
        {
            //Set the function to be called when a player clicks on the “create new model” button
            SetName(player);
        }
        /// <summary>
        /// Displays all properties of the pattern specified as parameter.
        /// The user can select one of the properties to make modifications.
        /// </summary>
        /// <param name="player">The player requesting to edit the pattern.</param>
        /// <param name="patternId">The ID of the pattern to be edited.</param>
        public async void EditPattern(Player player, int patternId)
        {
            Shop pattern = new Shop(false);
            pattern.Context = Context;
            await pattern.SetProperties(patternId);

            Panel panel = Context.PanelHelper.Create($"Modifier un {pattern.TypeName}", UIPanel.PanelType.Tab, player, () => EditPattern(player, patternId));


            panel.AddTabLine($"{mk.Color("Nom:", mk.Colors.Info)} {pattern.PatternName}", _ => {
                pattern.SetName(player, true);
            });
            panel.AddTabLine($"{mk.Color("Sociétés autorisées:", mk.Colors.Info)} {pattern.LBizAllowed.Count}", _ => {
                pattern.SetBizAllowed(player, true);
            });

            panel.NextButton("Sélectionner", () => panel.SelectTab());
            panel.PreviousButton();
            panel.CloseButton();

            panel.Display();
        }

        /// <summary>
        /// Allows the player to set a name for the pattern, either during creation or modification.
        /// </summary>
        /// <param name="player">The player interacting with the panel.</param>
        /// <param name="inEdition">A flag indicating if the pattern is being edited.</param>
        public void SetName(Player player, bool isEditing = false)
        {
            Panel panel = Context.PanelHelper.Create($"{(!isEditing ? "Créer" : "Modifier")} un modèle de {TypeName}", UIPanel.PanelType.Input, player, () => SetName(player));

            panel.TextLines.Add("Donner un nom à votre boutique");
            panel.inputPlaceholder = "3 caractères minimum";

            if (!isEditing)
            {
                panel.NextButton("Suivant", () =>
                {
                    if (panel.inputText.Length >= 3)
                    {
                        PatternName = panel.inputText;
                        LItems = new List<int>();
                        Items = JsonConvert.SerializeObject(LItems);
                        LBizAllowed = new List<int>();
                        SetBizAllowed(player, isEditing);
                    }
                    else
                    {
                        player.Notify("Attention", "Vous devez donner un titre à votre boutique (3 caractères minimum)", Life.NotificationManager.Type.Warning);
                        panel.Refresh();
                    }
                });
            }
            else
            {
                panel.PreviousButtonWithAction("Confirmer", async () =>
                {
                    if (panel.inputText.Length >= 3)
                    {
                        PatternName = panel.inputText;
                        if (await Save()) return true;
                        else
                        {
                            player.Notify("Erreur", "échec lors de la sauvegarde de vos changements", Life.NotificationManager.Type.Error);
                            return false;
                        }
                    }
                    else
                    {
                        player.Notify("Attention", "Vous devez donner un titre à votre boutique (3 caractères minimum)", Life.NotificationManager.Type.Warning);
                        return false;
                    }
                });
            }
            panel.PreviousButton();
            panel.CloseButton();

            panel.Display();
        }

        public void SetBizAllowed(Player player, bool isEditing = false)
        {
            Panel panel = Context.PanelHelper.Create($"{(!isEditing ? "Créer" : "Modifier")} un modèle de {TypeName}", UIPanel.PanelType.TabPrice, player, () => SetBizAllowed(player));

            foreach (var biz in Nova.biz.bizs)
            {
                bool isAllowed = LBizAllowed.Contains(biz.Id);
                panel.AddTabLine($"{mk.Color($"{biz.BizName}", isAllowed ? mk.Colors.Success : mk.Colors.Error)}", _ => {
                    if(isAllowed) LBizAllowed.Remove(biz.Id);
                    else LBizAllowed.Add(biz.Id);
                    SetBizAllowed(player, isEditing);
                });
            }

            panel.NextButton("Sélectionner", () => panel.SelectTab());
            if (!isEditing)
            {
                panel.NextButton("Sauvegarder", async () =>
                {
                    Shop newShop = new Shop();

                    newShop.TypeName = nameof(Shop);
                    newShop.PatternName = PatternName;
                    newShop.BizAllowed = ListConverter.WriteJson(LBizAllowed);
                    newShop.LItems = new List<int>();
                    newShop.Items = ListConverter.WriteJson(LItems);

                    //function to call for the following property
                    // If you want to generate your point
                    if (await newShop.Save())
                    {
                        player.Notify("PointShop", "Modifications enregistrées", NotificationManager.Type.Success);
                        ConfirmGeneratePoint(player, newShop);
                    }
                    else
                    {
                        player.Notify("PointShop", "Nous n'avons pas pu enregistrer vos modifications", NotificationManager.Type.Error);
                        panel.Refresh();
                    }
                });
            }
            else
            {
                panel.PreviousButtonWithAction("Sauvegarder", async () =>
                {
                    BizAllowed = ListConverter.WriteJson(LBizAllowed);
                    //function to call for the following property
                    // If you want to generate your point
                    if (await Save())
                    {
                        player.Notify("PointShop", "Modifications enregistrées", NotificationManager.Type.Success);
                        return true;
                    }
                    else
                    {
                        player.Notify("PointShop", "Nous n'avons pas pu enregistrer vos modifications", NotificationManager.Type.Error);
                        return false;
                    }
                    
                });
            }
            
            panel.PreviousButton();
            panel.CloseButton();

            panel.Display();
        }

        #region REPLACE YOUR CLASS/TYPE AS PARAMETER
        /// <summary>
        /// Displays a panel allowing the player to select a pattern from a list of patterns.
        /// </summary>
        /// <param name="player">The player selecting the pattern.</param>
        /// <param name="patterns">The list of patterns to choose from.</param>
        /// <param name="configuring">A flag indicating if the player is configuring.</param>
        public void SelectPattern(Player player, List<Shop> patterns, bool configuring)
        {
            Panel panel = Context.PanelHelper.Create("Choisir un modèle", UIPanel.PanelType.Tab, player, () => SelectPattern(player, patterns, configuring));

            foreach (var pattern in patterns)
            {
                panel.AddTabLine($"{pattern.PatternName}", _ => { });
            }
            if (patterns.Count == 0) panel.AddTabLine($"Vous n'avez aucun modèle de {TypeName}", _ => { });

            if (!configuring && patterns.Count != 0)
            {
                panel.CloseButtonWithAction("Confirmer", async () =>
                {
                    if (await Context.PointHelper.CreateNPoint(player, patterns[panel.selectedTab])) return true;
                    else return false;
                });
            }
            else
            {
                panel.NextButton("Modifier", () => {
                    EditPattern(player, patterns[panel.selectedTab].Id);
                });
                panel.NextButton("Supprimer", () => {
                    ConfirmDeletePattern(player, patterns[panel.selectedTab]);
                });
            }

            panel.AddButton("Retour", ui =>
            {
                AAMenu.AAMenu.menu.AdminPointsSettingPanel(player);
            });
            panel.CloseButton();

            panel.Display();
        }

        /// <summary>
        /// Confirms the generation of a point with a previously saved pattern.
        /// </summary>
        /// <param name="player">The player confirming the point generation.</param>
        /// <param name="pattern">The pattern to generate the point from.</param>
        public void ConfirmGeneratePoint(Player player, Shop pattern)
        {
            Panel panel = Context.PanelHelper.Create($"Modèle \"{pattern.PatternName}\" enregistré !", UIPanel.PanelType.Text, player, () =>
            ConfirmGeneratePoint(player, pattern));

            panel.TextLines.Add($"Voulez-vous générer un point sur votre position avec ce modèle \"{PatternName}\"");

            panel.CloseButtonWithAction("Générer", async () =>
            {
                if (await Context.PointHelper.CreateNPoint(player, pattern)) return true;
                else return false;
            });
            panel.PreviousButton();
            panel.CloseButton();

            panel.Display();
        }
        #endregion

        #region DO NOT EDIT
        /// <summary>
        /// Base panel allowing the user to choose between creating a pattern from scratch
        /// or generating a point from an existing pattern.
        /// </summary>
        /// <param name="player">The player initiating the creation or generation.</param>
        public void CreateOrGenerate(Player player)
        {
            Panel panel = Context.PanelHelper.Create($"Créer ou générer un {TypeName}", UIPanel.PanelType.Text, player, () => CreateOrGenerate(player));

            panel.TextLines.Add(mk.Pos($"{mk.Align($"{mk.Color("Générer", mk.Colors.Info)} utiliser un modèle existant. Les données sont partagés entre les points utilisant un même modèle.", mk.Aligns.Left)}", 5));
            panel.TextLines.Add("");
            panel.TextLines.Add($"{mk.Align($"{mk.Color("Créer:", mk.Colors.Info)} définir un nouveau modèle de A à Z.", mk.Aligns.Left)}");

            panel.NextButton("Créer", () =>
            {
                SetPatternData(player);
            });
            panel.NextButton("Générer", async () =>
            {
                await GetPatternData(player, false);
            });
            panel.AddButton("Retour", ui =>
            {
                AAMenu.AAMenu.menu.AdminPointsPanel(player);
            });
            panel.CloseButton();

            panel.Display();
        }

        /// <summary>
        /// Retrieves all patterns before redirecting to a panel allowing the user various actions (CRUD).
        /// </summary>
        /// <param name="player">The player initiating the retrieval of pattern data.</param>
        /// <param name="configuring">A flag indicating if the user is configuring.</param>
        public async Task GetPatternData(Player player, bool configuring)
        {
            var patterns = await QueryAll();
            SelectPattern(player, patterns, configuring);
        }

        /// <summary>
        /// Confirms the deletion of the specified pattern.
        /// </summary>
        /// <param name="player">The player confirming the deletion.</param>
        /// <param name="patternData">The pattern data to be deleted.</param>
        public async void ConfirmDeletePattern(Player player, PatternData patternData)
        {
            var pattern = await Query(patternData.Id);

            Panel panel = Context.PanelHelper.Create($"Supprimer un modèle de {pattern.TypeName}", UIPanel.PanelType.Text, player, () =>
            ConfirmDeletePattern(player, patternData));

            panel.TextLines.Add($"Cette suppression entrainera également celle des points.");
            panel.TextLines.Add($"Êtes-vous sûr de vouloir supprimer le modèle \"{pattern.PatternName}\" ?");

            panel.PreviousButtonWithAction("Confirmer", async () =>
            {
                if (await Context.PointHelper.DeleteNPointsByPattern(player, pattern))
                {
                    if (await pattern.Delete())
                    {
                        return true;
                    }
                    else
                    {
                        player.Notify("Erreur", $"Nous n'avons pas pu supprimer le modèle \"{PatternName}\"", Life.NotificationManager.Type.Error, 6);
                        return false;
                    }
                }
                else
                {
                    player.Notify("Erreur", "Certains points n'ont pas pu être supprimés.", Life.NotificationManager.Type.Error, 6);
                    return false;
                }
            });
            panel.PreviousButton();
            panel.CloseButton();

            panel.Display();
        }

        /// <summary>
        /// Retrieves all NPoints before redirecting to a panel allowing various actions by the user.
        /// </summary>
        /// <param name="player">The player retrieving the NPoints.</param>
        public async Task GetNPoints(Player player)
        {
            var points = await NPoint.Query(e => e.TypeName == nameof(Shop));
            SelectNPoint(player, points);
        }

        /// <summary>
        /// Lists the points using this pattern.
        /// </summary>
        /// <param name="player">The player selecting the points.</param>
        /// <param name="points">The list of points to choose from.</param>
        public async void SelectNPoint(Player player, List<NPoint> points)
        {
            var patterns = await QueryAll();
            Panel panel = Context.PanelHelper.Create($"Points de type {nameof(Shop)}", UIPanel.PanelType.Tab, player, () => SelectNPoint(player, points));

            if (points.Count > 0)
            {
                foreach (var point in points)
                {
                    var currentPattern = patterns.FirstOrDefault(p => p.Id == point.PatternId);
                    panel.AddTabLine($"point n° {point.Id}: {(currentPattern != default ? currentPattern.PatternName : "???")}", _ => { });
                }

                panel.NextButton("Voir", () =>
                {
                    DisplayNPoint(player, points[panel.selectedTab]);
                });
                panel.NextButton("Supprimer", async () =>
                {
                    await Context.PointHelper.DeleteNPoint(points[panel.selectedTab]);
                    await GetNPoints(player);
                });
            }
            else
            {
                panel.AddTabLine($"Aucun point de ce type", _ => { });
            }
            panel.AddButton("Retour", ui =>
            {
                AAMenu.AAMenu.menu.AdminPointsSettingPanel(player);
            });
            panel.CloseButton();

            panel.Display();
        }

        /// <summary>
        /// Displays the information of a point and allows the user to modify it.
        /// </summary>
        /// <param name="player">The player viewing the point information.</param>
        /// <param name="point">The point to display information for.</param>
        public async void DisplayNPoint(Player player, NPoint point)
        {
            var pattern = await Query(p => p.Id == point.PatternId);
            Panel panel = Context.PanelHelper.Create($"Point n° {point.Id}", UIPanel.PanelType.Tab, player, () => DisplayNPoint(player, point));

            panel.AddTabLine($"Type: {point.TypeName}", _ => { });
            panel.AddTabLine($"Modèle: {(pattern[0] != null ? pattern[0].PatternName : "???")}", _ => { });
            panel.AddTabLine($"", _ => { });
            panel.AddTabLine($"Position: {point.Position}", _ => { });


            panel.AddButton("TP", ui =>
            {
                Context.PointHelper.PlayerSetPositionToNPoint(player, point);
            });
            panel.AddButton("Définir pos.", async ui =>
            {
                await Context.PointHelper.SetNPointPosition(player, point);
                panel.Refresh();
            });
            panel.PreviousButton();
            panel.CloseButton();

            panel.Display();
        }
        #endregion
    }
}

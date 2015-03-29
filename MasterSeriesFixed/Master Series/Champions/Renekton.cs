﻿using System;
using System.Linq;
using Color = System.Drawing.Color;

using LeagueSharp;
using LeagueSharp.Common;
using SharpDX;

using Orbwalk = MasterSeries.Common.M_Orbwalker;

namespace MasterSeries.Champions
{
    class Renekton : Program
    {
        private Vector3 HarassBackPos = default(Vector3);
        private bool ECasted = false;
        private int AACount = 0;

        public Renekton()
        {
            Q = new Spell(SpellSlot.Q, 325);
            W = new Spell(SpellSlot.W, 300);
            E = new Spell(SpellSlot.E, 450);
            R = new Spell(SpellSlot.R, 20);
            Q.SetSkillshot(-0.5f, 0, 0, false, SkillshotType.SkillshotCircle);
            W.SetSkillshot(0.0435f, 0, 0, false, SkillshotType.SkillshotCircle);
            E.SetSkillshot(-0.5f, 50, 20, false, SkillshotType.SkillshotLine);

            var ChampMenu = new Menu("Plugin", Name + "Plugin");
            {
                var ComboMenu = new Menu("Combo", "Combo");
                {
                    ItemBool(ComboMenu, "Q", "Use Q");
                    ItemBool(ComboMenu, "W", "Use W");
                    ItemBool(ComboMenu, "E", "Use E");
                    ItemBool(ComboMenu, "Item", "Use Item");
                    ItemBool(ComboMenu, "Ignite", "Auto Ignite If Killable");
                    ChampMenu.AddSubMenu(ComboMenu);
                }
                var HarassMenu = new Menu("Harass", "Harass");
                {
                    ItemBool(HarassMenu, "Q", "Use Q");
                    ItemBool(HarassMenu, "W", "Use W");
                    ItemBool(HarassMenu, "E", "Use E");
                    ItemSlider(HarassMenu, "EAbove", "-> If Hp Above", 20);
                    ChampMenu.AddSubMenu(HarassMenu);
                }
                var ClearMenu = new Menu("Lane/Jungle Clear", "Clear");
                {
                    ItemBool(ClearMenu, "Q", "Use Q");
                    ItemBool(ClearMenu, "W", "Use W");
                    ItemBool(ClearMenu, "E", "Use E");
                    ItemBool(ClearMenu, "Item", "Use Tiamat/Hydra");
                    ChampMenu.AddSubMenu(ClearMenu);
                }
                var UltiMenu = new Menu("Ultimate", "Ultimate");
                {
                    ItemBool(UltiMenu, "RSurvive", "Try Use R To Survive");
                    ItemSlider(UltiMenu, "RUnder", "-> If Hp Under", 30);
                    ChampMenu.AddSubMenu(UltiMenu);
                }
                var MiscMenu = new Menu("Misc", "Misc");
                {
                    ItemBool(MiscMenu, "WAntiGap", "Use W To Anti Gap Closer");
                    ItemBool(MiscMenu, "WInterrupt", "Use W To Interrupt");
                    ItemBool(MiscMenu, "WCancel", "Cancel W Animation");
                    ItemSlider(MiscMenu, "CustomSkin", "Skin Changer", 6, 0, 6).ValueChanged += SkinChanger;
                    ChampMenu.AddSubMenu(MiscMenu);
                }
                var DrawMenu = new Menu("Draw", "Draw");
                {
                    ItemBool(DrawMenu, "Q", "Q Range", false);
                    ItemBool(DrawMenu, "E", "E Range", false);
                    ChampMenu.AddSubMenu(DrawMenu);
                }
                Config.AddSubMenu(ChampMenu);
            }
            Game.OnUpdate += OnUpdate;
            Drawing.OnDraw += OnDraw;
            AntiGapcloser.OnEnemyGapcloser += OnEnemyGapcloser;
            Interrupter.OnPossibleToInterrupt += OnPossibleToInterrupt;
            Obj_AI_Base.OnProcessSpellCast += OnProcessSpellCast;
            Orbwalk.AfterAttack += AfterAttack;
        }

        private void OnUpdate(EventArgs args)
        {
            if (Player.IsDead || MenuGUI.IsChatOpen || Player.IsChannelingImportantSpell() || Player.IsRecalling())
            {
                if (Player.IsDead) AACount = 0;
                return;
            }
            switch (Orbwalk.CurrentMode)
            {
                case Orbwalk.Mode.Combo:
                    NormalCombo();
                    break;
                case Orbwalk.Mode.Harass:
                    Harass();
                    break;
                case Orbwalk.Mode.LaneClear:
                    LaneJungClear();
                    break;
                case Orbwalk.Mode.LaneFreeze:
                    LaneJungClear();
                    break;
                case Orbwalk.Mode.Flee:
                    if (E.IsReady()) E.Cast(Game.CursorPos, PacketCast());
                    break;
            }
        }

        private void OnDraw(EventArgs args)
        {
            if (Player.IsDead) return;
            if (ItemBool("Draw", "Q") && Q.Level > 0) Utility.DrawCircle(Player.Position, Q.Range, Q.IsReady() ? Color.Green : Color.Red);
            if (ItemBool("Draw", "E") && E.Level > 0) Utility.DrawCircle(Player.Position, E.Range, E.IsReady() ? Color.Green : Color.Red);
        }

        private void OnEnemyGapcloser(ActiveGapcloser gapcloser)
        {
            if (!ItemBool("Misc", "WAntiGap") || Player.IsDead || !W.IsReady() || !Player.HasBuff("RenektonExecuteReady")) return;
            if (IsValid(gapcloser.Sender, Orbwalk.GetAutoAttackRange() + 50))
            {
                if (W.IsReady()) W.Cast(PacketCast());
                if (Player.HasBuff("RenektonExecuteReady")) Player.IssueOrder(GameObjectOrder.AttackUnit, gapcloser.Sender);
            }
        }

        private void OnPossibleToInterrupt(Obj_AI_Base unit, InterruptableSpell spell)
        {
            if (!ItemBool("Misc", "WInterrupt") || Player.IsDead || !W.IsReady() || !Player.HasBuff("RenektonExecuteReady")) return;
            if (E.IsReady() && !W.IsInRange(unit.Position) && IsValid(unit, E.Range)) E.Cast(unit.Position + Vector3.Normalize(unit.Position - Player.Position) * 200, PacketCast());
            if (IsValid(unit, Orbwalk.GetAutoAttackRange() + 50))
            {
                if (W.IsReady()) W.Cast(PacketCast());
                if (Player.HasBuff("RenektonExecuteReady")) Player.IssueOrder(GameObjectOrder.AttackUnit, unit);
            }
        }

        private void OnProcessSpellCast(Obj_AI_Base sender, GameObjectProcessSpellCastEventArgs args)
        {
            if (Player.IsDead) return;
            if (sender.IsMe)
            {
                if (Orbwalk.IsAutoAttack(args.SData.Name) && IsValid((Obj_AI_Base)args.Target) && W.IsReady())
                {
                    var Obj = (Obj_AI_Base)args.Target;
                    if ((Orbwalk.CurrentMode == Orbwalk.Mode.LaneClear || Orbwalk.CurrentMode == Orbwalk.Mode.LaneFreeze) && ItemBool("Clear", "W") && args.Target is Obj_AI_Minion && (CanKill(Obj, W, Player.Mana >= 50 ? 1 : 0) || Obj.MaxHealth >= 1200))
                    {
                        W.Cast(PacketCast());
                    }
                    else if ((Orbwalk.CurrentMode == Orbwalk.Mode.Combo || Orbwalk.CurrentMode == Orbwalk.Mode.Harass) && ItemBool(Orbwalk.CurrentMode.ToString(), "W") && args.Target is Obj_AI_Hero) W.Cast(PacketCast());
                }
                if (args.SData.Name == "RenektonCleave") AACount = 0;
                if (args.SData.Name == "RenektonPreExecute") AACount = 0;
                if (args.SData.Name == "RenektonSliceAndDice")
                {
                    ECasted = true;
                    Utility.DelayAction.Add((Orbwalk.CurrentMode == Orbwalk.Mode.LaneClear || Orbwalk.CurrentMode == Orbwalk.Mode.LaneFreeze) ? 3000 : 1800, () => ECasted = false);
                    AACount = 0;
                }
                if (args.SData.Name == "renektondice") AACount = 0;
            }
            else if (sender.IsEnemy && ItemBool("Ultimate", "RSurvive") && R.IsReady())
            {
                if (args.Target.IsMe && ((Orbwalk.IsAutoAttack(args.SData.Name) && (Player.Health - sender.GetAutoAttackDamage(Player, true)) * 100 / Player.MaxHealth <= ItemSlider("Ultimate", "RUnder")) || (args.SData.Name == "summonerdot" && (Player.Health - (sender as Obj_AI_Hero).GetSummonerSpellDamage(Player, Damage.SummonerSpell.Ignite)) * 100 / Player.MaxHealth <= ItemSlider("Ultimate", "RUnder"))))
                {
                    R.Cast(PacketCast());
                }
                else if ((args.Target.IsMe || (Player.Position.Distance(args.Start) <= args.SData.CastRange && Player.Position.Distance(args.End) <= Orbwalk.GetAutoAttackRange())) && Damage.Spells.ContainsKey((sender as Obj_AI_Hero).ChampionName))
                {
                    for (var i = 3; i > -1; i--)
                    {
                        if (Damage.Spells[(sender as Obj_AI_Hero).ChampionName].FirstOrDefault(a => a.Slot == (sender as Obj_AI_Hero).GetSpellSlot(args.SData.Name) && a.Stage == i) != null)
                        {
                            if ((Player.Health - (sender as Obj_AI_Hero).GetSpellDamage(Player, (sender as Obj_AI_Hero).GetSpellSlot(args.SData.Name), i)) * 100 / Player.MaxHealth <= ItemSlider("Ultimate", "RUnder")) R.Cast(PacketCast());
                        }
                    }
                }
            }
        }

        private void AfterAttack(Obj_AI_Base Unit, Obj_AI_Base Target)
        {
            if (!Unit.IsMe) return;
            if ((Orbwalk.CurrentMode == Orbwalk.Mode.Harass && Target is Obj_AI_Hero) || ((Orbwalk.CurrentMode == Orbwalk.Mode.LaneClear || Orbwalk.CurrentMode == Orbwalk.Mode.LaneFreeze) && Target is Obj_AI_Minion)) AACount += 1;
            if ((Orbwalk.CurrentMode == Orbwalk.Mode.Combo || Orbwalk.CurrentMode == Orbwalk.Mode.Harass) && ItemBool(Orbwalk.CurrentMode.ToString(), "W") && ItemBool("Misc", "WCancel") && Target.Buffs.Any(i => i.SourceName == Name && i.DisplayName == "Stun") && Target is Obj_AI_Hero) UseItem(Target, true);
        }

        private void NormalCombo()
        {
            if (targetObj == null) return;
            if (ItemBool("Combo", "W") && (W.IsReady() || Player.HasBuff("RenektonExecuteReady")) && !Player.IsDashing() && Orbwalk.InAutoAttackRange(targetObj))
            {
                Orbwalk.SetAttack(false);
                Player.IssueOrder(GameObjectOrder.AttackUnit, targetObj);
                Orbwalk.SetAttack(true);
            }
            if (ItemBool("Combo", "Q") && Q.IsReady() && !Player.IsDashing() && Q.IsInRange(targetObj.Position)) Q.Cast(PacketCast());
            if (ItemBool("Combo", "E") && E.IsReady() && E.IsInRange(targetObj.Position))
            {
                if (E.Instance.Name == "RenektonSliceAndDice")
                {
                    E.Cast(Player.Position.To2D().Extend(targetObj.Position.To2D(), targetObj.Distance3D(Player) + 200), PacketCast());
                }
                else if (!ECasted || !IsValid(targetObj, E.Range - 30) || CanKill(targetObj, E, Player.Mana >= 50 ? 1 : 0)) E.Cast(Player.Position.To2D().Extend(targetObj.Position.To2D(), targetObj.Distance3D(Player) + 200), PacketCast());
            }
            if (ItemBool("Combo", "Item")) UseItem(targetObj);
            if (ItemBool("Combo", "Ignite") && IgniteReady()) CastIgnite(targetObj);
        }

        private void Harass()
        {
            if (targetObj == null) return;
            if (ItemBool("Harass", "W") && (W.IsReady() || Player.HasBuff("RenektonExecuteReady")) && !Player.IsDashing() && (AACount >= 1 || (E.IsReady() && E.Instance.Name != "RenektonSliceAndDice")) && Orbwalk.InAutoAttackRange(targetObj))
            {
                Orbwalk.SetAttack(false);
                Player.IssueOrder(GameObjectOrder.AttackUnit, targetObj);
                Orbwalk.SetAttack(true);
            }
            if (ItemBool("Harass", "Q") && Q.IsReady() && !Player.IsDashing() && AACount >= 2 && Q.IsInRange(targetObj.Position)) Q.Cast(PacketCast());
            if (ItemBool("Harass", "E") && !Player.IsDashing())
            {
                if (E.IsReady())
                {
                    if (E.Instance.Name == "RenektonSliceAndDice")
                    {
                        if (E.IsInRange(targetObj.Position) && Player.HealthPercentage() >= ItemSlider("Harass", "EAbove"))
                        {
                            HarassBackPos = Player.ServerPosition;
                            E.Cast(Player.Position.To2D().Extend(targetObj.Position.To2D(), targetObj.Distance3D(Player) + 200), PacketCast());
                        }
                    }
                    else if (!ECasted || AACount >= 2) E.Cast(HarassBackPos, PacketCast());
                }
                else if (HarassBackPos != default(Vector3)) HarassBackPos = default(Vector3);
            }
        }

        private void LaneJungClear()
        {
            var minionObj = ObjectManager.Get<Obj_AI_Base>().Where(i => IsValid(i, E.Range) && i is Obj_AI_Minion).OrderBy(i => i.Health);
            foreach (var Obj in minionObj)
            {
                if (ItemBool("Clear", "Q") && Q.IsReady() && !Player.IsDashing() && (AACount >= 2 || (E.IsReady() && E.Instance.Name != "RenektonSliceAndDice")) && (minionObj.Count(i => IsValid(i, Q.Range)) >= 2 || (Obj.MaxHealth >= 1200 && Q.IsInRange(Obj.Position)))) Q.Cast(PacketCast());
                if (ItemBool("Clear", "W") && (W.IsReady() || Player.HasBuff("RenektonExecuteReady")) && !Player.IsDashing() && AACount >= 1 && Orbwalk.InAutoAttackRange(Obj) && (CanKill(Obj, W, Player.Mana >= 50 ? 1 : 0) || Obj.MaxHealth >= 1200))
                {
                    Orbwalk.SetAttack(false);
                    Player.IssueOrder(GameObjectOrder.AttackUnit, Obj);
                    Orbwalk.SetAttack(true);
                    break;
                }
                if (ItemBool("Clear", "E") && E.IsReady() && !Player.IsDashing())
                {
                    var posEFarm = E.GetLineFarmLocation(minionObj.ToList());
                    if (E.Instance.Name == "RenektonSliceAndDice")
                    {
                        E.Cast(posEFarm.MinionsHit >= 2 ? posEFarm.Position : Player.Position.To2D().Extend(Obj.Position.To2D(), Obj.Distance3D(Player) + 200), PacketCast());
                    }
                    else if (!ECasted || AACount >= 2) E.Cast(posEFarm.MinionsHit >= 2 ? posEFarm.Position : Player.Position.To2D().Extend(Obj.Position.To2D(), Obj.Distance3D(Player) + 200), PacketCast());
                }
                if (ItemBool("Clear", "Item") && AACount >= 1) UseItem(Obj, true);
            }
        }

        private void UseItem(Obj_AI_Base Target, bool IsFarm = false)
        {
            if (Items.CanUseItem(Tiamat) && IsFarm ? Player.Distance3D(Target) <= 350 : Player.CountEnemysInRange(350) >= 1) Items.UseItem(Tiamat);
            if (Items.CanUseItem(Hydra) && IsFarm ? Player.Distance3D(Target) <= 350 : (Player.CountEnemysInRange(350) >= 2 || (Player.GetAutoAttackDamage(Target, true) < Target.Health && Player.CountEnemysInRange(350) == 1))) Items.UseItem(Hydra);
            if (Items.CanUseItem(Randuin) && Player.CountEnemysInRange(450) >= 1 && !IsFarm) Items.UseItem(Randuin);
        }
    }
}
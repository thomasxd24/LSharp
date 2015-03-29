﻿using System;
using System.Linq;
using Color = System.Drawing.Color;

using LeagueSharp;
using LeagueSharp.Common;
using SharpDX;

using Orbwalk = MasterSeries.Common.M_Orbwalker;

namespace MasterSeries.Champions
{
    class Nasus : Program
    {
        private int Sheen = 3057, Iceborn = 3025, Trinity = 3078;

        public Nasus()
        {
            Q = new Spell(SpellSlot.Q, 300);
            W = new Spell(SpellSlot.W, 600);
            E = new Spell(SpellSlot.E, 650);
            R = new Spell(SpellSlot.R, 20);
            Q.SetSkillshot(0.0435f, 0, 0, false, SkillshotType.SkillshotCircle);
            W.SetTargetted(-0.5f, 0);
            E.SetSkillshot(-0.5f, 0, 20, false, SkillshotType.SkillshotCircle);

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
                    ChampMenu.AddSubMenu(HarassMenu);
                }
                var ClearMenu = new Menu("Lane/Jungle Clear", "Clear");
                {
                    var SmiteMob = new Menu("Smite Mob If Killable", "SmiteMob");
                    {
                        ItemBool(SmiteMob, "Baron", "Baron Nashor");
                        ItemBool(SmiteMob, "Dragon", "Dragon");
                        ItemBool(SmiteMob, "Red", "Red Brambleback");
                        ItemBool(SmiteMob, "Blue", "Blue Sentinel");
                        ItemBool(SmiteMob, "Krug", "Ancient Krug");
                        ItemBool(SmiteMob, "Gromp", "Gromp");
                        ItemBool(SmiteMob, "Raptor", "Crimson Raptor");
                        ItemBool(SmiteMob, "Wolf", "Greater Murk Wolf");
                        ClearMenu.AddSubMenu(SmiteMob);
                    }
                    ItemBool(ClearMenu, "Q", "Use Q");
                    ItemBool(ClearMenu, "E", "Use E");
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
                    ItemBool(MiscMenu, "QLastHit", "Use Q To Last Hit");
                    ItemBool(MiscMenu, "EKillSteal", "Use E To Kill Steal");
                    ItemSlider(MiscMenu, "CustomSkin", "Skin Changer", 5, 0, 5).ValueChanged += SkinChanger;
                    ChampMenu.AddSubMenu(MiscMenu);
                }
                var DrawMenu = new Menu("Draw", "Draw");
                {
                    ItemBool(DrawMenu, "W", "W Range", false);
                    ItemBool(DrawMenu, "E", "E Range", false);
                    ChampMenu.AddSubMenu(DrawMenu);
                }
                Config.AddSubMenu(ChampMenu);
            }
            Game.OnUpdate += OnUpdate;
            Drawing.OnDraw += OnDraw;
            Obj_AI_Base.OnProcessSpellCast += OnProcessSpellCast;
        }

        private void OnUpdate(EventArgs args)
        {
            if (Player.IsDead || MenuGUI.IsChatOpen || Player.IsChannelingImportantSpell() || Player.IsRecalling()) return;
            if (Orbwalk.CurrentMode == Orbwalk.Mode.Combo || Orbwalk.CurrentMode == Orbwalk.Mode.Harass)
            {
                NormalCombo(Orbwalk.CurrentMode.ToString());
            }
            else if (Orbwalk.CurrentMode == Orbwalk.Mode.LaneClear || Orbwalk.CurrentMode == Orbwalk.Mode.LaneFreeze)
            {
                LaneJungClear();
            }
            else if (Orbwalk.CurrentMode == Orbwalk.Mode.LastHit) LastHit();
            if (ItemBool("Misc", "EKillSteal")) KillSteal();
        }

        private void OnDraw(EventArgs args)
        {
            if (Player.IsDead) return;
            if (ItemBool("Draw", "W") && W.Level > 0) Utility.DrawCircle(Player.Position, W.Range, W.IsReady() ? Color.Green : Color.Red);
            if (ItemBool("Draw", "E") && E.Level > 0) Utility.DrawCircle(Player.Position, E.Range, E.IsReady() ? Color.Green : Color.Red);
        }

        private void OnProcessSpellCast(Obj_AI_Base sender, GameObjectProcessSpellCastEventArgs args)
        {
            if (Player.IsDead) return;
            if (sender.IsMe && Orbwalk.IsAutoAttack(args.SData.Name) && IsValid((Obj_AI_Base)args.Target) && Q.IsReady())
            {
                var Obj = (Obj_AI_Base)args.Target;
                var DmgAA = Player.GetAutoAttackDamage(Obj) * Math.Floor(Q.Instance.Cooldown / 1 / Player.AttackDelay);
                if (Orbwalk.CurrentMode == Orbwalk.Mode.LastHit && ItemBool("Misc", "QLastHit") && Q.GetHealthPrediction(Obj) + 5 <= GetBonusDmg(Obj) && (args.Target is Obj_AI_Minion || args.Target is Obj_AI_Turret))
                {
                    Q.Cast(PacketCast());
                }
                else if ((((Orbwalk.CurrentMode == Orbwalk.Mode.LaneClear || Orbwalk.CurrentMode == Orbwalk.Mode.LaneFreeze) && ItemBool("Clear", "Q") && (args.Target is Obj_AI_Minion || args.Target is Obj_AI_Turret)) || ((Orbwalk.CurrentMode == Orbwalk.Mode.Combo || Orbwalk.CurrentMode == Orbwalk.Mode.Harass) && ItemBool(Orbwalk.CurrentMode.ToString(), "Q") && args.Target is Obj_AI_Hero)) && (Q.GetHealthPrediction(Obj) + 5 <= GetBonusDmg(Obj) || (!(args.Target is Obj_AI_Turret) && Q.GetHealthPrediction(Obj) + 5 > DmgAA + GetBonusDmg(Obj)))) Q.Cast(PacketCast());
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

        private void NormalCombo(string Mode)
        {
            if (targetObj == null) return;
            if (ItemBool(Mode, "W") && W.IsReady() && W.IsInRange(targetObj.Position) && (Mode == "Combo" || (Mode == "Harass" && Player.Distance3D(targetObj) <= Orbwalk.GetAutoAttackRange() + 100))) W.CastOnUnit(targetObj, PacketCast());
            if (ItemBool(Mode, "E") && E.IsReady() && E.IsInRange(targetObj.Position) && (Mode == "Combo" || (Mode == "Harass" && Player.Distance3D(targetObj) <= Orbwalk.GetAutoAttackRange() + 100))) E.Cast(targetObj.Position, PacketCast());
            if (ItemBool(Mode, "Q") && Q.IsReady() && Player.Distance3D(targetObj) <= Orbwalk.GetAutoAttackRange() + 50)
            {
                var DmgAA = Player.GetAutoAttackDamage(targetObj) * Math.Floor(Q.Instance.Cooldown / 1 / Player.AttackDelay);
                if (Q.GetHealthPrediction(targetObj) + 5 <= GetBonusDmg(targetObj) || Q.GetHealthPrediction(targetObj) + 5 > DmgAA + GetBonusDmg(targetObj))
                {
                    Orbwalk.SetAttack(false);
                    Player.IssueOrder(GameObjectOrder.AttackUnit, targetObj);
                    Orbwalk.SetAttack(true);
                }
            }
            if (Mode == "Combo" && ItemBool(Mode, "Item") && Items.CanUseItem(Randuin) && Player.CountEnemysInRange(450) >= 1) Items.UseItem(Randuin);
            if (Mode == "Combo" && ItemBool(Mode, "Ignite") && IgniteReady()) CastIgnite(targetObj);
        }

        private void LaneJungClear()
        {
            var minionObj = ObjectManager.Get<Obj_AI_Base>().Where(i => IsValid(i, E.Range) && (i is Obj_AI_Turret || i is Obj_AI_Minion)).OrderBy(i => i.MaxHealth).Reverse();
            foreach (var Obj in minionObj)
            {
                if (SmiteReady() && Obj.Team == GameObjectTeam.Neutral)
                {
                    if ((ItemBool("SmiteMob", "Baron") && Obj.Name.StartsWith("SRU_Baron")) || (ItemBool("SmiteMob", "Dragon") && Obj.Name.StartsWith("SRU_Dragon")) || (!Obj.Name.Contains("Mini") && (
                        (ItemBool("SmiteMob", "Red") && Obj.Name.StartsWith("SRU_Red")) || (ItemBool("SmiteMob", "Blue") && Obj.Name.StartsWith("SRU_Blue")) ||
                        (ItemBool("SmiteMob", "Krug") && Obj.Name.StartsWith("SRU_Krug")) || (ItemBool("SmiteMob", "Gromp") && Obj.Name.StartsWith("SRU_Gromp")) ||
                        (ItemBool("SmiteMob", "Raptor") && Obj.Name.StartsWith("SRU_Razorbeak")) || (ItemBool("SmiteMob", "Wolf") && Obj.Name.StartsWith("SRU_Murkwolf"))))) CastSmite(Obj);
                }
                if (ItemBool("Clear", "E") && E.IsReady() && minionObj.Count(i => i is Obj_AI_Minion) > 0)
                {
                    var posEFarm = E.GetCircularFarmLocation(minionObj.Where(i => i is Obj_AI_Minion).ToList());
                    E.Cast(posEFarm.MinionsHit >= 2 ? posEFarm.Position : minionObj.First(i => i is Obj_AI_Minion).Position.To2D(), PacketCast());
                }
                if (ItemBool("Clear", "Q") && Q.IsReady() && Player.Distance3D(Obj) <= Orbwalk.GetAutoAttackRange() + 50)
                {
                    var DmgAA = Player.GetAutoAttackDamage(Obj) * Math.Floor(Q.Instance.Cooldown / 1 / Player.AttackDelay);
                    if (Q.GetHealthPrediction(Obj) + 5 <= GetBonusDmg(targetObj) || Q.GetHealthPrediction(Obj) + 5 > DmgAA + GetBonusDmg(Obj))
                    {
                        Orbwalk.SetAttack(false);
                        Player.IssueOrder(GameObjectOrder.AttackUnit, Obj);
                        Orbwalk.SetAttack(true);
                        break;
                    }
                }
            }
        }

        private void LastHit()
        {
            if (!ItemBool("Misc", "QLastHit") || !Q.IsReady()) return;
            foreach (var Obj in ObjectManager.Get<Obj_AI_Base>().Where(i => IsValid(i, Orbwalk.GetAutoAttackRange() + 50) && Q.GetHealthPrediction(i) + 5 <= GetBonusDmg(i) && (i is Obj_AI_Minion || i is Obj_AI_Turret)).OrderBy(i => i.MaxHealth).Reverse().OrderBy(i => i.Distance3D(Player)))
            {
                Orbwalk.SetAttack(false);
                Player.IssueOrder(GameObjectOrder.AttackUnit, Obj);
                Orbwalk.SetAttack(true);
                break;
            }
        }

        private void KillSteal()
        {
            if (!E.IsReady()) return;
            foreach (var Obj in ObjectManager.Get<Obj_AI_Hero>().Where(i => IsValid(i, E.Range) && CanKill(i, E) && i != targetObj).OrderBy(i => i.Health).OrderByDescending(i => i.Distance3D(Player))) E.Cast(Obj.Position, PacketCast());
        }

        private double GetBonusDmg(Obj_AI_Base Target)
        {
            double DmgItem = 0;
            if (Items.HasItem(Sheen) && ((Items.CanUseItem(Sheen) && Q.IsReady()) || Player.HasBuff("Sheen")) && Player.BaseAttackDamage > DmgItem) DmgItem = Player.BaseAttackDamage;
            if (Items.HasItem(Iceborn) && ((Items.CanUseItem(Iceborn) && Q.IsReady()) || Player.HasBuff("ItemFrozenFist")) && Player.BaseAttackDamage * 1.25 > DmgItem) DmgItem = Player.BaseAttackDamage * 1.25;
            if (Items.HasItem(Trinity) && ((Items.CanUseItem(Trinity) && Q.IsReady()) || Player.HasBuff("Sheen")) && Player.BaseAttackDamage * 2 > DmgItem) DmgItem = Player.BaseAttackDamage * 2;
            return (Q.IsReady() ? Q.GetDamage(Target) : 0) + Player.GetAutoAttackDamage(Target, Q.IsReady() ? false : true) + Player.CalcDamage(Target, Damage.DamageType.Physical, DmgItem);
        }
    }
}
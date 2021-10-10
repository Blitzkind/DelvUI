﻿using Dalamud.Game.ClientState.JobGauge.Enums;
using Dalamud.Game.ClientState.JobGauge.Types;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using DelvUI.Config;
using DelvUI.Config.Attributes;
using DelvUI.Helpers;
using DelvUI.Interface.Bars;
using FFXIVClientStructs.FFXIV.Client.Game.Gauge;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using DelvUI.Enums;
using DelvUI.Interface.GeneralElements;

namespace DelvUI.Interface.Jobs
{
    public class AstrologianHud : JobHud
    {
        private readonly SpellHelper _spellHelper = new();
        private new AstrologianConfig Config => (AstrologianConfig)_config;
        private static PluginConfigColor EmptyColor => GlobalColors.Instance.EmptyColor;

        private static readonly List<uint> DotIDs = new() { 1881, 843, 838 };
        private static readonly List<float> DotDuration = new() { 30f, 30f, 18f };
        private const float StarMaxDuration = 10f;
        private const float LightspeedMaxDuration = 15f;

        public AstrologianHud(JobConfig config, string? displayName = null) : base(config, displayName)
        {
        }

        protected override (List<Vector2>, List<Vector2>) ChildrenPositionsAndSizes()
        {
            List<Vector2> positions = new();
            List<Vector2> sizes = new();

            if (Config.DrawBar.Enabled)
            {
                positions.Add(Config.Position + Config.DrawBar.Position);
                sizes.Add(Config.DrawBar.Size);
            }

            if (Config.DivinationBar.Enabled)
            {
                positions.Add(Config.Position + Config.DivinationBar.Position);
                sizes.Add(Config.DivinationBar.Size);
            }

            if (Config.DotBar.Enabled)
            {
                positions.Add(Config.Position + Config.DotBar.Position);
                sizes.Add(Config.DotBar.Size);
            }

            if (Config.StarBar.Enabled)
            {
                positions.Add(Config.Position + Config.StarBar.Position);
                sizes.Add(Config.StarBar.Size);
            }

            if (Config.LightspeedBar.Enabled)
            {
                positions.Add(Config.Position + Config.LightspeedBar.Position);
                sizes.Add(Config.LightspeedBar.Size);
            }

            return (positions, sizes);
        }

        public override void DrawJobHud(Vector2 origin, PlayerCharacter player)
        {
            Vector2 pos = origin + Config.Position;

            if (Config.DivinationBar.Enabled)
            {
                DrawDivinationBar(pos, player);
            }

            if (Config.DrawBar.Enabled)
            {
                DrawDraw(pos);
            }

            if (Config.DotBar.Enabled)
            {
                DrawDot(pos, player);
            }

            if (Config.LightspeedBar.Enabled)
            {
                DrawLightspeed(pos, player);
            }

            if (Config.StarBar.Enabled)
            {
                DrawStar(pos, player);
            }
        }

        private string RedrawText(float redrawCastInfo, int redrawStacks = 0)
        {
            string redrawStacksString = Config.DrawBar.ShowRedrawStacks ? redrawStacks.ToString("N0", CultureInfo.InvariantCulture) : "()";
            if (redrawCastInfo < 0 || !Config.DrawBar.ShowRedrawCooldown)
            {
                return Config.DrawBar.ShowRedrawStacks ? "(" +redrawStacksString+")":"";
            }

            if (!Config.DrawBar.EnableRedrawCooldownCumulated)
            {
                if (redrawCastInfo % 30 == 0)
                {
                    redrawCastInfo = 30f;
                }

                redrawCastInfo %= 30f;
            }

            string format = Config.DrawBar.EnableDecimalRedrawBar ? "N1" : "N0";
            return Config.DrawBar.ShowRedrawStacks ? redrawCastInfo.ToString(format, CultureInfo.InvariantCulture) + " (" + redrawStacksString + ")" : redrawCastInfo.ToString(format, CultureInfo.InvariantCulture);

        }

        private unsafe void DrawDivinationBar(Vector2 origin, PlayerCharacter player)
        {
            List<PluginConfigColor> chunkColors = new();
            ASTGauge gauge = Plugin.JobGauges.Get<ASTGauge>();
            IntPtr gaugeAddress = gauge.Address;
            byte[] sealsFromBytes = new byte[3];
            bool[] chucksToGlow = new bool[3];

            AstrologianGauge* tmp = (AstrologianGauge*)gaugeAddress;
            for (int ix = 0; ix < 3; ++ix)
            {
                sealsFromBytes[ix] = tmp->Seals[ix];
            }
            
            for (int ix = 0; ix < 3; ++ix)
            {
                byte seal = sealsFromBytes[ix];
                SealType type = (SealType)seal;

                switch (type)
                {
                    case SealType.NONE:
                        chunkColors.Add(EmptyColor);

                        break;

                    case SealType.MOON:
                        chunkColors.Add(Config.DivinationBar.SealLunarColor);

                        break;

                    case SealType.SUN:
                        chunkColors.Add(Config.DivinationBar.SealSunColor);

                        break;

                    case SealType.CELESTIAL:
                        chunkColors.Add(Config.DivinationBar.SealCelestialColor);

                        break;
                }

                int sealNumbers = 0;
                Config.DivinationBar.Label.SetText("");

                if (gauge.ContainsSeal(SealType.NONE))
                {
                    continue;
                }

                if (gauge.ContainsSeal(SealType.SUN))
                {
                    sealNumbers++;
                }

                if (gauge.ContainsSeal(SealType.MOON))
                {
                    sealNumbers++;
                }

                if (gauge.ContainsSeal(SealType.CELESTIAL))
                {
                    sealNumbers++;
                }

                Config.DivinationBar.Label.SetText(sealNumbers.ToString());
                

                for (int i = 0; i < sealNumbers; i++)
                {
                    chucksToGlow[i] = true;
                }

            }

            if (chunkColors.All(n => n == EmptyColor) && Config.DivinationBar.HideWhenInactive)
            {
                return;
            }

            Tuple<PluginConfigColor, float, LabelConfig?>[] divinationChunks = { new(chunkColors[0], 1f, null), new(chunkColors[1], 1f, Config.DivinationBar.Label), new(chunkColors[2], 1f, null) };
            BarUtilities.GetChunkedBars(Config.DivinationBar, player, Config.DivinationBar.DivinationGlowColor, chucksToGlow, divinationChunks).Draw(origin);

        }

        private void DrawDraw(Vector2 origin)
        {
            ASTGauge gauge = Plugin.JobGauges.Get<ASTGauge>();

            string cardJob = "";
            PluginConfigColor cardColor = EmptyColor;

            if (gauge.DrawnCard == CardType.NONE && Config.DrawBar.HideWhenInactive)
            {
                return;
            }

            switch (gauge.DrawnCard)
            {
                case CardType.BALANCE:
                    cardColor = Config.DivinationBar.SealSunColor;
                    cardJob = "MELEE";
                    Config.DrawBar.DrawGlowConfig.Color = new PluginConfigColor(Config.DrawBar.DrawMeleeGlowColor.Vector);
                    break;

                case CardType.BOLE:
                    cardColor = Config.DivinationBar.SealSunColor;
                    cardJob = "RANGED";
                    Config.DrawBar.DrawGlowConfig.Color = new PluginConfigColor(Config.DrawBar.DrawRangedGlowColor.Vector);
                    break;

                case CardType.ARROW:
                    cardColor = Config.DivinationBar.SealLunarColor;
                    cardJob = "MELEE";
                    Config.DrawBar.DrawGlowConfig.Color = new PluginConfigColor(Config.DrawBar.DrawMeleeGlowColor.Vector);
                    break;

                case CardType.EWER:
                    cardColor = Config.DivinationBar.SealLunarColor;
                    cardJob = "RANGED";
                    Config.DrawBar.DrawGlowConfig.Color = new PluginConfigColor(Config.DrawBar.DrawRangedGlowColor.Vector);
                    break;

                case CardType.SPEAR:
                    cardColor = Config.DivinationBar.SealCelestialColor;
                    cardJob = "MELEE";
                    Config.DrawBar.DrawGlowConfig.Color = new PluginConfigColor(Config.DrawBar.DrawMeleeGlowColor.Vector);
                    break;

                case CardType.SPIRE:
                    cardColor = Config.DivinationBar.SealCelestialColor;
                    cardJob = "RANGED";
                    Config.DrawBar.DrawGlowConfig.Color = new PluginConfigColor(Config.DrawBar.DrawRangedGlowColor.Vector);
                    break;

                case CardType.NONE:
                    Config.DrawBar.DrawGlowConfig.Color = new PluginConfigColor(Vector4.Zero);
                    break;
            }

            float cardPresent;
            float cardMax;
            float drawCastInfo = _spellHelper.GetSpellCooldown(3590);
            float redrawCastInfo = _spellHelper.GetSpellCooldown(3593);
            int redrawStacks = _spellHelper.GetStackCount(3, 3593);

            if (cardJob != "")
            {
                cardPresent = 1f;
                cardMax = 1f;
                Config.DrawBar.Label.SetText(cardJob);
                Config.DrawBar.DrawDrawLabel.SetText(Config.DrawBar.ShowDrawCardWhileDrawn?drawCastInfo > 0 ? Math.Abs(drawCastInfo).ToString(Config.DrawBar.EnableDecimalDrawBar ? "N1" : "N0", CultureInfo.InvariantCulture) : "READY":"");
            }
            else
            {
                cardPresent = drawCastInfo > 0 ? drawCastInfo : 1f;
                Config.DrawBar.Label.SetText(drawCastInfo > 0 ? Math.Abs(drawCastInfo).ToString(Config.DrawBar.EnableDecimalDrawBar ? "N1" : "N0", CultureInfo.InvariantCulture) : "READY");
                Config.DrawBar.DrawDrawLabel.SetText("");
                cardColor = drawCastInfo > 0 ? Config.DrawBar.DrawCdColor : Config.DrawBar.DrawCdReadyColor;
                cardMax = drawCastInfo > 0 ? 30f : 1f;
            }
            
            Config.DrawBar.DrawRedrawLabel.SetText(RedrawText(redrawCastInfo, redrawStacks));
            BarUtilities.GetBar(Config.DrawBar, cardPresent, cardMax, 0f, Player, cardColor, Config.DrawBar.DrawGlowConfig.Enabled && Math.Abs(cardMax - 1f) == 0f  ? Config.DrawBar.DrawGlowConfig : null, Config.DrawBar.Label, Config.DrawBar.DrawRedrawLabel, Config.DrawBar.DrawDrawLabel).Draw(origin);

        }

        private void DrawDot(Vector2 origin, PlayerCharacter player)
        {
            GameObject? target = Plugin.TargetManager.SoftTarget ?? Plugin.TargetManager.Target;
            BarUtilities.GetDoTBar(Config.DotBar, player, target, DotIDs, DotDuration)?.Draw(origin);
        }

        private void DrawLightspeed(Vector2 origin, BattleChara player)
        {
            float lightspeedDuration = player.StatusList.FirstOrDefault(o => o.StatusId is 841 && o.SourceID == player.ObjectId)?.RemainingTime ?? 0f;

            if (Config.LightspeedBar.HideWhenInactive && !(lightspeedDuration > 0))
            {
                return;
            }
            
            Config.LightspeedBar.Label.SetText($"{lightspeedDuration.ToString(Config.LightspeedBar.EnableDecimalLightspeedBar ? "N1" : "N0", CultureInfo.InvariantCulture)}");
            BarUtilities.GetProgressBar(Config.LightspeedBar, lightspeedDuration, LightspeedMaxDuration).Draw(origin);
        }

        private void DrawStar(Vector2 origin, BattleChara player)
        {
            float starPreCookingBuff = player.StatusList.FirstOrDefault(o => o.StatusId is 1224 && o.SourceID == player.ObjectId)?.RemainingTime ?? 0f;
            float starPostCookingBuff = player.StatusList.FirstOrDefault(o => o.StatusId is 1248 && o.SourceID == player.ObjectId)?.RemainingTime ?? 0f;
            
            if (Config.StarBar.HideWhenInactive && starPostCookingBuff == 0f && starPreCookingBuff == 0f)
            {
                return;
            }

            float currentStarDuration = starPreCookingBuff > 0 ? StarMaxDuration - Math.Abs(starPreCookingBuff) : Math.Abs(starPostCookingBuff);
            PluginConfigColor currentStarColor = starPreCookingBuff > 0 ? Config.StarBar.StarEarthlyColor : Config.StarBar.StarGiantColor;
            Config.StarBar.Label.SetText($"{currentStarDuration.ToString(Config.StarBar.EnableDecimalStarBar ? "N1" : "N0", CultureInfo.InvariantCulture)}");
            BarUtilities.GetProgressBar(Config.StarBar, currentStarDuration, StarMaxDuration, 0f, player, currentStarColor, Config.StarBar.StarGlowConfig.Enabled && starPostCookingBuff > 0? Config.StarBar.StarGlowConfig : null).Draw(origin); // Star Countdown after Star is ready 

        }
    }

    [Section("Job Specific Bars")]
    [SubSection("Healer", 0)]
    [SubSection("Astrologian", 1)]
    public class AstrologianConfig : JobConfig
    {
        [JsonIgnore] public override uint JobId => JobIDs.AST;
        private AstrologianConfig() { UseDefaultPrimaryResourceBar = true; }
        public new static AstrologianConfig DefaultConfig() => new();

        [NestedConfig("Draw Bar", 100)]
        public AstrologianDrawBarConfig DrawBar = new(
            new Vector2(0, -32),
            new Vector2(254, 20),
            new PluginConfigColor(new Vector4(255f / 255f, 255f / 255f, 255f / 255f, 0f / 100f))
        );

        [NestedConfig("Divination Bar", 200)]
        public AstrologianDivinationBarConfig DivinationBar = new (
            new Vector2(0, -71),
            new Vector2(254, 10)
        );

        [NestedConfig("Dot Bar", 300)]
        public AstrologianDotBarConfig DotBar = new(
            new Vector2(-85, -54),
            new Vector2(84, 20),
            new PluginConfigColor(new Vector4(20f / 255f, 80f / 255f, 168f / 255f, 255f / 100f))
        );

        [NestedConfig("Star Bar", 400)]
        public AstrologianStarBarConfig StarBar = new(
            new Vector2(0, -54),
            new Vector2(84, 20)
        );

        [NestedConfig("Lightspeed Bar", 500)]
        public AstrologianLightspeedBarConfig LightspeedBar = new(
            new Vector2(85, -54),
            new Vector2(84, 20),
            new PluginConfigColor(new Vector4(255f / 255f, 255f / 255f, 173f / 255f, 100f / 100f))
        );

        [Exportable(false)]
        public class AstrologianDrawBarConfig : ProgressBarConfig
        {

            [NestedConfig("Draw Side Timer Label" + "##Draw", 101, separator = false, spacing = true)]
            public LabelConfig DrawDrawLabel = new(new Vector2(0, 0), "", DrawAnchor.Left, DrawAnchor.Left);

            [Checkbox("Enable Draw Decimal Precision" + "##Draw")]
            [Order(102)]
            public bool EnableDecimalDrawBar;

            [Checkbox("Show Drawn Timer while a Card is Drawn" + "##Draw")]
            [Order(103)]
            public bool ShowDrawCardWhileDrawn;
            
            [NestedConfig("Redraw Timer Label" + "##Draw", 104, separator = false, spacing = true)]
            public LabelConfig DrawRedrawLabel = new(new Vector2(0,0),"", DrawAnchor.Right, DrawAnchor.Right);

            [Checkbox("Redraw Stacks" + "##Redraw")]
            [Order(105)]
            public bool ShowRedrawStacks = true;

            [Checkbox("Show Redraw Cooldown" + "##Redraw")]
            [Order(106)]
            public bool ShowRedrawCooldown;

            [Checkbox("Enable Redraw Decimal Precision" + "##Redraw")]
            [Order(107)]
            public bool EnableDecimalRedrawBar;

            [Checkbox("Total Redraw Cooldown Instead of Next" + "##Redraw")]
            [Order(108)]
            public bool EnableRedrawCooldownCumulated;

            [ColorEdit4("Draw on CD" + "##Draw")]
            [Order(109)]
            public PluginConfigColor DrawCdColor = new(new Vector4(26f / 255f, 167f / 255f, 109f / 255f, 100f / 100f));

            [ColorEdit4("Draw Ready" + "##Draw")]
            [Order(110)]
            public PluginConfigColor DrawCdReadyColor = new(new Vector4(137f / 255f, 26f / 255f, 42f / 255f, 100f / 100f));


            [NestedConfig("Card Preferred Target with Glow" + "##Divination", 111, separator = false, spacing = true)]
            //[DisableParentSettings("Color")]
            public BarGlowConfig DrawGlowConfig = new();

            [ColorEdit4("Melee Glow" + "##Draw")]
            [Order(112)]
            public PluginConfigColor DrawMeleeGlowColor = new(new Vector4(83f / 255f, 34f / 255f, 120f / 255f, 100f / 100f));

            [ColorEdit4("Ranged Glow" + "##Draw")]
            [Order(113)]
            public PluginConfigColor DrawRangedGlowColor = new(new Vector4(124f / 255f, 34f / 255f, 120f / 255f, 100f / 100f));

            public AstrologianDrawBarConfig(Vector2 position, Vector2 size, PluginConfigColor fillColor)
                : base(position, size, fillColor)
            {
            }
        }

        [DisableParentSettingsAttribute("FillColor")] // <= Doesn't work.
        [Exportable(false)]
        public class AstrologianDivinationBarConfig : ChunkedProgressBarConfig
        {

            [ColorEdit4("Sun" + "##Divination")]
            [Order(201)]
            public PluginConfigColor SealSunColor = new(new Vector4(213f / 255f, 124f / 255f, 97f / 255f, 100f / 100f));

            [ColorEdit4("Lunar" + "##Divination")]
            [Order(202)]
            public PluginConfigColor SealLunarColor = new(new Vector4(241f / 255f, 217f / 255f, 125f / 255f, 100f / 100f));

            [ColorEdit4("Celestial" + "##Divination")]
            [Order(203)]
            public PluginConfigColor SealCelestialColor = new(new Vector4(100f / 255f, 207f / 255f, 211f / 255f, 100f / 100f));

            [NestedConfig("Glow" + "##Divination", 205, separator = false, spacing = true)]
            public BarGlowConfig DivinationGlowColor = new();

            public AstrologianDivinationBarConfig(Vector2 position, Vector2 size)
                : base(position, size, new PluginConfigColor(Vector4.Zero))
            {
            }
        }

        [Exportable(false)]
        //[DisableParentSettingsAttribute("Label")]
        public class AstrologianDotBarConfig : ProgressBarConfig
        {
            // TODO: Implement EnableDecimalDotBar in DotBar
            [Checkbox("Enable Decimal Precision" + "##Combust")]
            [Order(301)]
            public bool EnableDecimalDotBar;
            public AstrologianDotBarConfig(Vector2 position, Vector2 size, PluginConfigColor fillColor)
                : base(position, size, fillColor)
            {
            }
        }

        [DisableParentSettingsAttribute("FillColor")]
        [Exportable(false)]
        public class AstrologianStarBarConfig : ProgressBarConfig
        {
            [Checkbox("Enable Decimal Precision" + "##Star")]
            [Order(401)]
            public bool EnableDecimalStarBar;

            [ColorEdit4("Earthly" + "##Star")]
            [Order(402)]
            public PluginConfigColor StarEarthlyColor = new(new Vector4(37f / 255f, 181f / 255f, 177f / 255f, 100f / 100f));

            [ColorEdit4("Giant" + "##Star")]
            [Order(403)]
            public PluginConfigColor StarGiantColor = new(new Vector4(198f / 255f, 154f / 255f, 199f / 255f, 100f / 100f));

            [NestedConfig("Giant Dominance Glow" + "##Star", 404, separator = false, spacing = true)]
            public BarGlowConfig StarGlowConfig = new();
            public AstrologianStarBarConfig(Vector2 position, Vector2 size)
                : base(position, size, new PluginConfigColor(Vector4.Zero))
            {
            }
        }

        [Exportable(false)]
        public class AstrologianLightspeedBarConfig : ProgressBarConfig
        {
            [Checkbox("Enable Decimal Precision" + "##Lightspeed")]
            [Order(501)]
            public bool EnableDecimalLightspeedBar;
            public AstrologianLightspeedBarConfig(Vector2 position, Vector2 size, PluginConfigColor fillColor)
                : base(position, size, fillColor)
            {
            }
        }
    }
}

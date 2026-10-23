using System;
using System.Numerics;
using Celeste.Core.Components;
using Celeste.Core.Display;
using Celeste.Core.Mathematics;
using Celeste.Core.Rendering;
using Celeste.Game.Components.Talking;
using Celeste.Game.Execution.Levels;
using Celeste.Graphics.Rendering;

namespace Celeste.Mods.GravityHelper.Entities.WatchTowers
{
    public class UpsideDownTalkComponentUI : OriginalTalkUiEntity
    {
        public UpsideDownTalkComponentUI(OriginalTalkComponent handler) : base(handler)
        {
        }

        public override void Render(ISourceRenderContext context)
        {
            var level = (OriginalLevelSceneRoot)Scene;
            if (level.FrozenOrPaused || slide <= 0f || Handler.Entity == null) return;
            Vector2 camera = Vector2Geometry.Floor(level.Camera.Position);
            Vector2 at = ((ISourceEntityPosition)Handler.Entity).Position + Handler.DrawAt - camera;
            if (Process.CurrentSave != null && Process.CurrentSave.Assists.MirrorMode) at.X = 320f - at.X;
            at.X *= 6f;
            at.Y *= 6f;
            at.Y -= (float)Math.Sin(timer * 4f) * 12f + 64f * (1f - Process.EasingArithmetic.CubeOut(slide));
            float num = !Highlighted ? 1f + wiggler.Value * .5f : 1f - wiggler.Value * .5f;
            float scale = Process.EasingArithmetic.CubeInOut(slide) * alpha;
            PackedColor color = lineColor * scale;
            var texture = Highlighted ? Handler.HoverUI.Texture : Process.GuiAtlas.Entries["hover/idle"];
            SourceAtlasDrawing.DrawJustified(context, this, texture, at, new Vector2(.5f, 1f), color * alpha,
                new Vector2(num, -num));
            if (Highlighted)
            {
                Vector2 position = at - Handler.HoverUI.InputPosition * num + Vector2.UnitY;
                if (Input.GuiInputController())
                    SourceAtlasDrawing.DrawJustified(context, this,
                        Input.GuiButton(Input.Talk,
                            Celeste.Game.Controls.Source.OriginalGameInputProcess.PrefixMode.Latest,
                            "controls/keyboard/oemquestion"), position, new Vector2(.5f), PackedColor.White * scale,
                        num);
                else
                    Process.ActiveFont.DrawOutline(context, this, Input.FirstKey(Input.Talk).ToString().ToUpper(),
                        position,
                        new Vector2(.5f), new Vector2(num), PackedColor.White * scale, 2f, PackedColor.Black);
            }
        }
    }
}

// Copyright (c) Shane Woolcock. Licensed under the MIT Licence.

using System;
using System.Collections;
using System.Numerics;
using Celeste.Core.Audio;
using Celeste.Core.Components;
using Celeste.Core.Components.Timing;
using Celeste.Core.Display;
using Celeste.Core.Mathematics;
using Celeste.Core.Mathematics.Extrema;
using Celeste.Core.Mathematics.LinearAlgebra;
using Celeste.Core.Randomness;
using Celeste.Core.Rendering;
using Celeste.Core.Reflection.Runtime;
using Celeste.Core.Scheduling;
using Celeste.Everest.Modules.Entities.Metadata;
using Celeste.Formats.Map.Entities.Structure;
using Celeste.Game.Content.Maps.Entities;
using Celeste.Game.Content.Maps.Levels;
using Celeste.Game.Controls.Source;
using Celeste.Game.Execution.Levels;
using Celeste.Game.Gameplay.Effects.Slashes;
using Celeste.Graphics.Animation;
using Celeste.Graphics.Content;
using Celeste.Graphics.Rendering;
using Celeste.Mods.GravityHelper.Components;
using Celeste.Mods.GravityHelper.Components.Accessibility;
using Celeste.Mods.GravityHelper.Configuration;
using Celeste.Mods.GravityHelper.Gravity;
using Celeste.Mods.GravityHelper.Metadata;
using Celeste.Mods.GravityHelper.Presentation;
using Celeste.Mods.GravityHelper.Presentation.Colors;
using Celeste.Mods.GravityHelper.Presentation.Shaders;
using Celeste.Mods.GravityHelper.Runtime;
using Celeste.Scene.Components.Oscillation;
using Celeste.Scene.Entities;
using Celeste.Scene.Tracking;
using Celeste.Physics.Collision.Shapes;
using Celeste.Simulation;
using Celeste.Simulation.Gameplay.Player.Collision.Components;
using Celeste.Simulation.Presentation.Effects.Mirrors;
using Celeste.Simulation.Presentation.Lighting;
using Celeste.Simulation.Presentation.PostProcessing.Bloom;
using Celeste.Simulation.Presentation.ScreenEffects;

namespace Celeste.Mods.GravityHelper.Entities.Collectibles
{
    public partial class GravityRefill
    {
        public override void Render(ISourceRenderContext context)
        {
            if (_isWall)
            {
                var camera = ((OriginalLevelSceneRoot)Scene).Camera;
                if (Right < (double)camera.Left || Left > (double)camera.Right || Bottom < (double)camera.Top ||
                    Top > (double)camera.Bottom)
                    return;

                getColors(out var fillColor, out var borderColor);

                _spriteAlpha = 1f;

                switch (_wallRenderMode)
                {
                    case WallRenderMode.Flash when !OneUse:
                    case WallRenderMode.Filled when !OneUse:
                        // draw outline 1 pixel out
                        SourcePrimitiveDrawing.HollowRect(context, this, X - 1f, Y - 1f, Width + 2f, Height + 2f,
                            borderColor);

                        // draw filled rectangle 1 pixel in
                        SourcePrimitiveDrawing.Rect(context, this, X + 1f, Y + 1f, Width - 2f, Height - 2f, fillColor);

                        break;

                    case WallRenderMode.Flash when OneUse:
                    case WallRenderMode.Filled when OneUse:
                        // draw a translucent outline 1 pixel out
                        SourcePrimitiveDrawing.HollowRect(context, this, X - 1f, Y - 1f, Width + 2f, Height + 2f,
                            borderColor * 0.6f);

                        // draw horizontal segments 1 pixel out
                        for (int index = 0; index < (double)Width; index += 8)
                        {
                            DrawLine(context,
                                (TopLeft - Vector2.UnitY + Vector2.UnitX * (index + 2)),
                                (TopLeft - Vector2.UnitY + Vector2.UnitX * (index + 6)),
                                borderColor);
                            DrawLine(context,
                                (BottomLeft + Vector2.UnitX * (index + 2)),
                                (BottomLeft + Vector2.UnitX * (index + 6)),
                                borderColor);
                        }

                        // draw vertical segments 1 pixel out
                        for (int index = 0; index < (double)Height; index += 8)
                        {
                            DrawLine(context,
                                (TopLeft + Vector2.UnitY * (index + 2)),
                                (TopLeft + Vector2.UnitY * (index + 6)),
                                borderColor);
                            DrawLine(context,
                                (TopRight + Vector2.UnitX + Vector2.UnitY * (index + 2)),
                                (TopRight + Vector2.UnitX + Vector2.UnitY * (index + 6)),
                                borderColor);
                        }

                        // draw filled rectangle 1 pixel in
                        SourcePrimitiveDrawing.Rect(context, this, X + 1f, Y + 1f, Width - 2f, Height - 2f, fillColor);

                        break;

                    case WallRenderMode.Returning:
                        // draw horizontal segments
                        for (int index = 0; index < (double)Width; index += 8)
                        {
                            DrawLine(context,
                                (TopLeft + Vector2.UnitX * (index + 2)),
                                (TopLeft + Vector2.UnitX * (index + 6)),
                                borderColor);
                            DrawLine(context,
                                (BottomLeft - Vector2.UnitY + Vector2.UnitX * (index + 2)),
                                (BottomLeft - Vector2.UnitY + Vector2.UnitX * (index + 6)),
                                borderColor);
                        }

                        // draw vertical segments
                        for (int index = 0; index < (double)Height; index += 8)
                        {
                            DrawLine(context,
                                (TopLeft + Vector2.UnitX + Vector2.UnitY * (index + 2)),
                                (TopLeft + Vector2.UnitX + Vector2.UnitY * (index + 6)),
                                borderColor);
                            DrawLine(context,
                                (TopRight + Vector2.UnitY * (index + 2)),
                                (TopRight + Vector2.UnitY * (index + 6)),
                                borderColor);
                        }

                        // set alpha on the sprites
                        _spriteAlpha = 0.25f;

                        break;
                }
            }

            using (GravityTintShader.For(runtime).Enter(context))
            {
                if (_sprite.Visible)
                {
                    var opaqueColor = runtime.Settings.ColorSchemeType ==
                                      ColorSchemeSetting.Default
                        ? PackedColor.White
                        : runtime.Settings.GetColorScheme().ToggleColor;

                    _sprite.Color = opaqueColor * _spriteAlpha;
                    if (_arrows != null) _arrows.Color = _sprite.Color;
                    _sprite.DrawOutline(context);
                }

                base.Render(context);
            }
        }

        private void DrawLine(ISourceRenderContext context, Vector2 from, Vector2 to, PackedColor color)
            => SourcePrimitiveDrawing.Line(context, this,
                new Vector2((float)Math.Round(from.X), (float)Math.Round(from.Y)),
                new Vector2((float)Math.Round(to.X), (float)Math.Round(to.Y)), color,
                ((ISourceVectorEnvironment)Engine).VectorArithmetic);

        private void getColors(out PackedColor fillColor, out PackedColor borderColor)
        {
            bool isDefault =
                runtime.Settings.ColorSchemeType is ColorSchemeSetting.Default;

            switch (_wallRenderMode)
            {
                case WallRenderMode.Filled:
                    if (Dashes == 2)
                    {
                        borderColor = PackedColor.Orchid;
                        fillColor = PackedColor.DarkOrchid;
                    }
                    else if (!RefillsDash)
                    {
                        borderColor = PackedColor.LightSkyBlue;
                        fillColor = PackedColor.CornflowerBlue;
                    }
                    else if (isDefault)
                    {
                        borderColor = PackedColor.BlueViolet;
                        fillColor = PackedColor.Indigo;
                    }
                    else
                    {
                        borderColor = runtime.Settings.GetColorScheme().ToggleColor;
                        fillColor = borderColor.Darker();
                    }

                    break;

                case WallRenderMode.Flash:
                    fillColor = borderColor = PackedColor.White;
                    break;

                case WallRenderMode.Returning:
                    borderColor = PackedColor.Gray * 0.25f;
                    fillColor = PackedColor.Transparent;
                    break;

                default:
                    throw new ArgumentOutOfRangeException();
            }

            borderColor *= _wallAlpha;
            fillColor *= _wallAlpha;
        }

        private enum WallRenderMode
        {
            Filled,
            Flash,
            Returning,
        }
    }
}

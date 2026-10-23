using System.Numerics;
using Celeste.Core.Components;
using Celeste.Core.Display;
using Celeste.Core.Rendering;
using Celeste.Graphics.Content;
using Celeste.Graphics.Content.Atlas;
using Celeste.Graphics.Rendering;
using Celeste.Mods.GravityHelper.Configuration;
using Celeste.Mods.GravityHelper.Gravity;
using Celeste.Scene.Entities;

namespace Celeste.Mods.GravityHelper.Entities.Springs
{
    [global::Celeste.Core.Reflection.Runtime.RuntimeTypeAlias(
        "Celeste.Mod.GravityHelper.Entities.GravitySpring+IndicatorRenderer")]
    public class GravitySpringIndicatorRenderer : SceneEntityState
    {
        private readonly GravitySpring spring;
        private AtlasEntry arrowTexture;
        private Vector2 arrowOrigin;

        public GravitySpringIndicatorRenderer(GravitySpring spring)
            : base(((ISourceEntitySourceDataEnvironment)spring.Engine).EntitySourceDataRegistration)
        {
            this.spring = spring;
            UpdateIndicator();
            Depth = -10501;
            Active = false;
            Collidable = false;
        }

        public void UpdateIndicator()
        {
            string prefix =
                spring.GravityType switch { GravityType.Normal => "down", GravityType.Inverted => "up", _ => "double" };
            var type = spring.Runtime.Settings.SpringArrowType;
            if (type == ArrowSetting.Default)
                type = !spring._showIndicator ? ArrowSetting.None :
                    spring._largeIndicator ? ArrowSetting.Large : ArrowSetting.Small;
            if (type == ArrowSetting.None) Visible = false;
            else
            {
                Visible = true;
                string size = type == ArrowSetting.Large ? "" : "Small";
                string path = string.IsNullOrWhiteSpace(spring._indicatorTexture)
                    ? "objects/GravityHelper/gravityField/" + prefix + "Arrow" + size
                    : spring._indicatorTexture;
                arrowTexture = ((ISourceGameGraphicsEnvironment)spring.Engine).GameGraphics.GameAtlas[path];
                arrowOrigin = new Vector2(arrowTexture.FrameWidth / 2f, arrowTexture.FrameHeight / 2f);
            }
        }

        public override void Render(ISourceRenderContext context)
        {
            if (arrowTexture == null) return;
            int offset = spring._indicatorOffset;
            Vector2 position = spring.Position + (spring.Orientation switch
            {
                GravitySpringOrientation.WallLeft => -Vector2.UnitX * offset,
                GravitySpringOrientation.WallRight => Vector2.UnitX * offset,
                GravitySpringOrientation.Ceiling => -Vector2.UnitY * offset,
                GravitySpringOrientation.Floor => Vector2.UnitY * offset,
                _ => Vector2.Zero
            }) + spring._indicatorShakeOffset;
            for (int x = -1; x <= 1; x++)
            for (int y = -1; y <= 1; y++)
                if (x != 0 || y != 0)
                    SourceAtlasDrawing.Draw(context, this, arrowTexture, position + new Vector2(x, y), arrowOrigin,
                        PackedColor.Black);
            SourceAtlasDrawing.Draw(context, this, arrowTexture, position, arrowOrigin, PackedColor.White);
        }
    }
}

// Copyright (c) Shane Woolcock. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Celeste.Core.Components;
using Celeste.Core.Rendering;
using Celeste.Core.Audio;
using Celeste.Game.Gameplay.Platforms.MoveBlocks;
using Celeste.Graphics.Animation;
using Celeste.Graphics.Content;
using Celeste.Mods.GravityHelper.Interop;
using Celeste.Simulation;

namespace Celeste.Mods.GravityHelper.Components
{
    internal sealed class MoveBlockBottomComponent : SourceComponentState
    {
        private new MoveBlockState Entity => base.Entity as MoveBlockState;
        public readonly List<SpriteImage> Images = new();
        public bool BottomPressed;

        public MoveBlockBottomComponent() : base(true, false)
        {
        }

        public override void Added(object entity)
        {
            base.Added(entity);
            if (entity is not MoveBlockState block) return;
            var engine = MovingBlockSourceLoans.World(block).EngineContext;
            var texture = ((ISourceGameGraphicsEnvironment)engine).GameGraphics.GameAtlas["objects/moveBlock/button"];
            int numTiles = (int)(block.Width / 8);
            for (int index = 0; index < numTiles; index++)
            {
                int tileX = index == 0 ? 0 : index < numTiles - 1 ? 1 : 2;
                MovingBlockSourceLoans.AddImage(block, texture.GetSubtexture(tileX * 8, 0, 8, 8),
                    new Vector2(index * 8f, block.Height + 4f), 0f, new Vector2(1, -1), Images);
            }

            var body = MovingBlockSourceLoans.Body(block);
            var top = body.Where(image => image.Position.Y <= 8).OrderBy(image => image.Position.X).ToArray();
            var bottom = body.Where(image => image.Position.Y >= block.Height - 8).OrderBy(image => image.Position.X)
                .ToArray();
            for (int i = 0; i < top.Length; i++)
            {
                bottom[i].Texture = top[i].Texture;
                bottom[i].FlipY = true;
            }

            foreach (var image in Images) image.Position.Y = Entity.Height;
        }

        public override void Update()
        {
            bool collide = Entity.CollideCheck(typeof(RoomPlayerSession).FullName, Entity.Position + Vector2.UnitY);
            foreach (var image in Images) image.Position.Y = Entity.Height + (collide ? -2f : 0f);
            if (collide && !BottomPressed) Play("event:/game/04_cliffside/arrowblock_side_depress");
            if (!collide && BottomPressed) Play("event:/game/04_cliffside/arrowblock_side_release");
            BottomPressed = collide;
        }

        private void Play(string sound)
        {
            var engine = MovingBlockSourceLoans.World(Entity).EngineContext;
            ((ISourceSpatialAudioEnvironment)engine).SpatialAudio.Play(sound, Entity.Position);
        }

        public override void Render(ISourceRenderContext context)
        {
            foreach (var image in Images)
            {
                image.Color = Entity.FillColor;
                image.Render(context);
            }
        }
    }
}

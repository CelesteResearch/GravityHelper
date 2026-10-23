using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Xml;
using Celeste.Core.Scheduling;
using Celeste.Core.Randomness;
using Celeste.Core.Text;
using Celeste.Formats.Graphics.Sprites;
using Celeste.Formats.Graphics.Xml;
using Celeste.Graphics.Animation;
using Celeste.Graphics.Content;
using Celeste.Graphics.Content.Atlas;
using Celeste.Graphics.Content.Xml;
using System.Numerics;

namespace Celeste.Mods.GravityHelper.Presentation
{
    public static class GravitySpriteTemplates
    {
        private sealed class FrameSource(AtlasData atlas) : IAnimationFrameSource
        {
            public AtlasEntry Frame(string key, int index) => atlas.Frame(key, index);
            public List<AtlasEntry> Frames(string key) => atlas.Frames(key);
        }

        private static readonly ConditionalWeakTable<AtlasData, FrameSource> Sources = new();

        public static Sprite CreateWithPath(ISourceEngineContext engine, string id, string path) =>
            CreateOnWithPath(engine, AtlasSpriteFactory.Create(engine, null, ""), id, path);

        public static Sprite CreateOnWithPath(ISourceEngineContext engine, Sprite sprite, string id, string path)
        {
            var bank = (SpriteBank)((ISourceGameGraphicsEnvironment)engine).GameGraphics.SpriteFactory;
            if (!bank.SpriteData.TryGetValue(id, out var data))
                throw new Exception($"Missing animation name in SpriteData: '{id}'!");
            return CreateOnWithPath(data, sprite, path);
        }

        private static Sprite CreateOnWithPath(SpriteData self, Sprite sprite, string overridePath)
        {
            if (self.Sources.FirstOrDefault() is not { } spriteDataSource)
            {
                System.Diagnostics.Trace.TraceWarning("CreateOnWithPath: Couldn't get sprite source.");
                return null;
            }

            overridePath = overridePath.TrimEnd('/', ' ') + "/";

            sprite.Reset(Sources.GetValue(self.Atlas, atlas => new FrameSource(atlas)), overridePath);
            float defaultDelay = spriteDataSource.XML.AttrFloat("delay", 0f);

            foreach (XmlElement animXml in spriteDataSource.XML.GetElementsByTagName("Anim"))
            {
                Chooser<string> into = !animXml.HasAttr("goto")
                    ? null
                    : Chooser<string>.FromString<string>(animXml.Attr("goto"));
                string id = animXml.Attr("id");
                string pathAttr = animXml.Attr("path", "");
                int[] frames = SourceCsvIntegerFrames.Read(animXml.Attr("frames", ""));
                sprite.Add(id, pathAttr, animXml.AttrFloat("delay", defaultDelay), into, frames);
            }

            foreach (XmlElement loopXml in spriteDataSource.XML.GetElementsByTagName("Loop"))
            {
                string id = loopXml.Attr("id");
                string pathAttr = loopXml.Attr("path", "");
                int[] frames = SourceCsvIntegerFrames.Read(loopXml.Attr("frames", ""));
                sprite.AddLoop(id, pathAttr, loopXml.AttrFloat("delay", defaultDelay), frames);
            }

            if (spriteDataSource.XML.HasChild("Center"))
            {
                sprite.CenterOrigin();
                sprite.Justify = new Vector2(0.5f, 0.5f);
            }
            else if (spriteDataSource.XML.HasChild("Justify"))
            {
                sprite.JustifyOrigin(spriteDataSource.XML.ChildPosition("Justify"));
                sprite.Justify = spriteDataSource.XML.ChildPosition("Justify");
            }
            else if (spriteDataSource.XML.HasChild("Origin"))
                sprite.Origin = spriteDataSource.XML.ChildPosition("Origin");

            if (spriteDataSource.XML.HasChild("Position"))
                sprite.Position = spriteDataSource.XML.ChildPosition("Position");
            if (spriteDataSource.XML.HasAttr("start"))
                sprite.Play(spriteDataSource.XML.Attr("start"));
            return sprite;
        }
    }
}

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
using WiiPlayTanksRemake.Graphics;
using WiiPlayTanksRemake.Internals;
using WiiPlayTanksRemake.Internals.Common.Framework.Audio;
using WiiPlayTanksRemake.Internals.Common.Utilities;

namespace WiiPlayTanksRemake.GameContent
{
    public sealed class Mine
    {
        private static int maxMines = 500;
        public static Mine[] AllMines { get; } = new Mine[maxMines];

        public Tank owner;

        public Vector2 Position;

        public Matrix View;
        public Matrix Projection;
        public Matrix World;

        public Vector3 Position3D => Position.ExpandZ();

        public Model Model;

        public static Texture2D _mineTexture;
        public static Texture2D _envTexture;

        private int worldId;

        public ModelMesh MineMesh;
        public ModelMesh EnvMesh;

        public Rectangle hitbox;

        public int detonationTime;
        public int detonationTimeMax;

        public bool tickRed;

        /// <summary>The radius of this <see cref="Mine"/>'s explosion.</summary>
        public float explosionRadius;

        /// <summary>Whether or not this <see cref="Mine"/> has detonated.</summary>
        public bool Detonated { get; set; }

        public int mineReactTime = 60;

        /// <summary>
        /// Creates a new <see cref="Mine"/>.
        /// </summary>
        /// <param name="owner">The <see cref="Tank"/> which owns this <see cref="Mine"/>.</param>
        /// <param name="pos">The position of this <see cref="Mine"/> in the game world.</param>
        /// <param name="detonateTime">The time it takes for this <see cref="Mine"/> to detonate.</param>
        /// <param name="radius">The radius of this <see cref="Mine"/>'s explosion.</param>
        public Mine(Tank owner, Vector2 pos, int detonateTime, float radius = 80f)
        {
            this.owner = owner;
            explosionRadius = radius;

            Model = GameResources.GetGameResource<Model>("Assets/mine");

            detonationTime = detonateTime;
            detonationTimeMax = detonateTime;

            Position = pos;

            MineMesh = Model.Meshes["polygon1"];
            EnvMesh = Model.Meshes["polygon0"];

            _mineTexture = GameResources.GetGameResource<Texture2D>("Assets/textures/mine/mine_env");
            _envTexture = GameResources.GetGameResource<Texture2D>("Assets/textures/mine/mine_shadow");

            int index = Array.IndexOf(AllMines, AllMines.First(mine => mine is null));

            worldId = index;

            AllMines[index] = this;
        }

        /// <summary>Detonates this <see cref="Mine"/>.</summary>
        public void Detonate()
        {
            Detonated = true;

            var expl = new Explosion(Position, explosionRadius * 0.101f, 0.3f);

            if (UI.DifficultyModes.UltraMines)
                expl.maxScale *= 2f;

            expl.expanseRate = 2f;
            expl.tickAtMax = 15;
            expl.shrinkRate = 0.5f;

            if (owner != null)
                owner.OwnedMineCount--;

            AllMines[worldId] = null;
        }

        public void RemoveSilently() => AllMines[worldId] = null;

        internal void Update()
        {
            World = Matrix.CreateScale(0.7f) * Matrix.CreateTranslation(Position3D);
            View = TankGame.GameView;
            Projection = TankGame.GameProjection;

            hitbox = new((int)Position.X - 10, (int)Position.Y - 10, 20, 20); 

            detonationTime--;

            if (detonationTime < 120)
            {
                if (detonationTime % 2 == 0)
                    tickRed = !tickRed;
            }

            if (detonationTime <= 0)
                Detonate();

            foreach (var shell in Shell.AllShells)
            {
                if (shell is not null && shell.hitbox.Intersects(hitbox))
                {
                    shell.Destroy();
                    Detonate();
                }
            }

            if (detonationTime > mineReactTime && detonationTime < detonationTimeMax / 2)
            {
                foreach (var tank in GameHandler.AllTanks)
                {
                    if (tank is not null && Vector2.Distance(tank.Position, Position) < explosionRadius * 9f)
                    {
                        detonationTime = mineReactTime;
                    }
                }
            }
        }

        internal void Render()
        {
            foreach (ModelMesh mesh in Model.Meshes)
            {
                foreach (BasicEffect effect in mesh.Effects)
                {
                    effect.World = World;
                    effect.View = View;
                    effect.Projection = Projection;

                    effect.TextureEnabled = true;

                    if (mesh == MineMesh)
                    {
                        if (!tickRed)
                        {
                            effect.EmissiveColor = new(1, 1, 0);
                            //effect.SpecularColor = new(1, 1, 0);
                            //effect.FogColor = new(1, 1, 0);
                        }
                        else
                        {
                            effect.EmissiveColor = new(1, 0, 0);
                            //effect.SpecularColor = new(1, 0, 0);
                            //effect.FogColor = new(1, 0, 0);
                        }
                        effect.Texture = _mineTexture;
                    }
                    else
                    {
                        effect.Texture = _envTexture;
                    }
                    effect.SetDefaultGameLighting_IngameEntities();
                }
                mesh.Draw();
            }
        }
    }
    internal class Explosion
    {
        // model, blah blah blah

        public const int MINE_EXPLOSIONS_MAX = 500;

        public static Explosion[] explosions = new Explosion[MINE_EXPLOSIONS_MAX];

        public Vector2 Position;

        public Vector3 Position3D => Position.ExpandZ();

        public Matrix View;
        public Matrix Projection;
        public Matrix World;

        public Model Model;

        public static Texture2D mask;

        public float scale;

        public float maxScale;

        public float expanseRate = 1f;
        public float shrinkRate = 1f;

        public int tickAtMax = 40;

        private bool hitMaxAlready;

        private int id;

        public float rotation;

        public float rotationSpeed;

        public Explosion(Vector2 pos, float scaleMax, float rotationSpeed = 1f)
        {
            this.rotationSpeed = rotationSpeed;
            Position = pos;
            maxScale = scaleMax;
            mask = GameResources.GetGameResource<Texture2D>(/*"Assets/textures/mine/explosion_mask"*/"Assets/textures/misc/tank_smoke_ami");

            Model = GameResources.GetGameResource<Model>("Assets/mineexplosion");

            int index = Array.IndexOf(explosions, explosions.First(t => t is null));

            var destroysound = GameResources.GetGameResource<SoundEffect>($"Assets/sounds/tnk_destroy");

            SoundPlayer.PlaySoundInstance(destroysound, SoundContext.Effect, 0.4f);

            id = index;

            explosions[index] = this;
        }

        public void Update()
        {
            if (!hitMaxAlready)
            {
                if (scale < maxScale)
                    scale += expanseRate;

                if (scale > maxScale)
                    scale = maxScale;

                if (scale >= maxScale)
                    hitMaxAlready = true;
            }
            else if (tickAtMax <= 0) 
                scale -= shrinkRate;

            foreach (var mine in Mine.AllMines)
            {
                if (mine is not null && Vector2.Distance(mine.Position, Position) <= scale * 9) // magick
                    mine.Detonate();
            }
            foreach (var cube in Block.AllBlocks)
            {
                if (cube is not null && Vector2.Distance(cube.Position, Position) <= scale * 9 && cube.IsDestructible)
                    cube.Destroy();
            }
            foreach (var shell in Shell.AllShells)
            {
                if (shell is not null && Vector2.Distance(shell.Position2D, Position) < scale * 9)
                    shell.Destroy();
            }
            foreach (var tank in GameHandler.AllTanks)
            {
                if (tank is not null && Vector2.Distance(tank.Position, Position) < scale * 9)
                    if (!tank.Dead)
                        if (tank.VulnerableToMines)
                            tank.Destroy();
            }

            if (hitMaxAlready)
                tickAtMax--;

            if (scale <= 0)
                explosions[id] = null;

            rotation += rotationSpeed;

            World = Matrix.CreateScale(scale) * Matrix.CreateRotationY(rotation) * Matrix.CreateTranslation(Position3D);
            View = TankGame.GameView;
            Projection = TankGame.GameProjection;
        }

        public void Render()
        {
            foreach (ModelMesh mesh in Model.Meshes)
            {
                foreach (BasicEffect effect in mesh.Effects)
                {
                    effect.World = World;
                    effect.View = View;
                    effect.Projection = Projection;
                    effect.TextureEnabled = true;

                    effect.Texture = mask;

                    effect.SetDefaultGameLighting_IngameEntities();

                    if (tickAtMax <= 0)
                        effect.Alpha -= 0.05f;
                    else
                        effect.Alpha = 1f;
                }
                mesh.Draw();
            }
        }
    }
}
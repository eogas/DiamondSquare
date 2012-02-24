using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.GamerServices;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Media;

namespace DiamondSquare
{
	public class DiamondSquare : Microsoft.Xna.Framework.Game
	{
		GraphicsDeviceManager graphics;
		SpriteBatch spriteBatch;
		RasterizerState rasterState;

		VertexPositionNormalTexture[] verts;
		int[] indices;

		Random random;
		int roughness = 4;
		int gridSize = 257;
		int seed = 0;
		
		Matrix worldMatrix;
		Matrix viewMatrix;
		Matrix projectionMatrix;
		BasicEffect basicEffect;

		float rotation = 0.0f;
		bool keyDown = false;
		bool doRotate = true;

		public DiamondSquare()
		{
			graphics = new GraphicsDeviceManager(this);
			Content.RootDirectory = "Content";

			graphics.PreferredBackBufferWidth = 768;
			graphics.PreferredBackBufferHeight = 512;
		}

		protected override void Initialize()
		{
			rasterState = new RasterizerState();
			rasterState.CullMode = CullMode.None;
			rasterState.FillMode = FillMode.Solid;

			random = new Random();
			seed = random.Next(100);

			InitShaders();
			GenerateTerrain();

			base.Initialize();
		}

		// TODO: break these off into a util class
		private int RandRange(Random r, int rMin, int rMax)
		{
			return rMin + r.Next() * (rMax - rMin);
		}

		private double RandRange(Random r, double rMin, double rMax)
		{
			return rMin + r.NextDouble() * (rMax - rMin);
		}

		private float RandRange(Random r, float rMin, float rMax)
		{
			return rMin + (float)r.NextDouble() * (rMax - rMin);
		}

		// Returns true if a is a power of 2, else false
		private bool pow2(int a)
		{
			return (a & (a - 1)) == 0;
		}

		/*
		 *	Generates a grid of VectorPositionColor elements as a 2D greyscale representation of terrain by the
		 *	Diamond-square algorithm: http://en.wikipedia.org/wiki/Diamond-square_algorithm
		 * 
		 *	Arguments: 
		 *		int size - the width or height of the grid being passed in.  Should be of the form (2 ^ n) + 1
		 *		int seed - an optional seed for the random generator
		 *		float rMin/rMax - the min and max height values for the terrain (defaults to 0 - 255 for greyscale)
		 *		float noise - the roughness of the resulting terrain
		 * */
		private float[][] DiamondSquareGrid(int size, int seed = 0, float rMin = 0, float rMax = 255, float noise = 0.0f)
		{
			// Fail if grid size is not of the form (2 ^ n) - 1 or if min/max values are invalid
			int s = size - 1;
			if (!pow2(s) || rMin >= rMax)
				return null;

			float modNoise = 0.0f;

			// init the grid
			float[][] grid = new float[size][];
			for (int i = 0; i < size; i++)
				grid[i] = new float[size];

			// Seed the first four corners
			Random rand = (seed == 0 ? new Random() : new Random(seed));
			grid[0][0] = RandRange(rand, rMin, rMax);
			grid[s][0] = RandRange(rand, rMin, rMax);
			grid[0][s] = RandRange(rand, rMin, rMax);
			grid[s][s] = RandRange(rand, rMin, rMax);

			/*
			 * Use temporary named variables to simplify equations
			 * 
			 * s0 . d0. s1
			 *  . . . . . 
			 * d1 . cn. d2
			 *  . . . . . 
			 * s2 . d3. s3
			 * 
			 * */
			float s0, s1, s2, s3, d0, d1, d2, d3, cn;

			for (int i = s; i > 1; i /= 2)
			{
				// reduce the random range at each step
				modNoise = (rMax - rMin) * noise * ((float)i / s);

				// diamonds
				for (int y = 0; y < s; y += i)
				{
					for (int x = 0; x < s; x += i)
					{
						s0 = grid[x][y];
						s1 = grid[x + i][y];
						s2 = grid[x][y + i];
						s3 = grid[x + i][y + i];

						// cn
						grid[x + (i / 2)][y + (i / 2)] = ((s0 + s1 + s2 + s3) / 4.0f)
							+ RandRange(rand, -modNoise, modNoise);
					}
				}

				// squares
				for (int y = 0; y < s; y += i)
				{
					for (int x = 0; x < s; x += i)
					{
						s0 = grid[x][y];
						s1 = grid[x + i][y];
						s2 = grid[x][y + i];
						s3 = grid[x + i][y + i];
						cn = grid[x + (i / 2)][y + (i / 2)];

						d0 = y <= 0 ? (s0 + s1 + cn) / 3.0f : (s0 + s1 + cn + grid[x + (i / 2)][y - (i / 2)]) / 4.0f;
						d1 = x <= 0 ? (s0 + cn + s2) / 3.0f : (s0 + cn + s2 + grid[x - (i / 2)][y + (i / 2)]) / 4.0f;
						d2 = x >= s - i ? (s1 + cn + s3) / 3.0f :
							(s1 + cn + s3 + grid[x + i + (i / 2)][y + (i / 2)]) / 4.0f;
						d3 = y >= s - i ? (cn + s2 + s3) / 3.0f :
							(cn + s2 + s3 + grid[x + (i / 2)][y + i + (i / 2)]) / 4.0f;

						grid[x + (i / 2)][y] = d0 + RandRange(rand, -modNoise, modNoise);
						grid[x][y + (i / 2)] = d1 + RandRange(rand, -modNoise, modNoise);
						grid[x + i][y + (i / 2)] = d2 + RandRange(rand, -modNoise, modNoise);
						grid[x + (i / 2)][y + i] = d3 + RandRange(rand, -modNoise, modNoise);
					}
				}
			}

			return grid;
		}

		private void GenerateTerrain()
		{
			// TODO: Get rid of some of these magic numbers
			float[][] ds = DiamondSquareGrid(gridSize, seed, 0, 40, roughness / 10.0f);
			Console.WriteLine("Roughness: {0}", roughness / 10.0f);

			verts = new VertexPositionNormalTexture[gridSize * gridSize];
			for (int y = 0; y < gridSize; y++)
			{
				for (int x = 0; x < gridSize; x++)
				{
					verts[x + y * gridSize] = new VertexPositionNormalTexture(
						new Vector3((x - (gridSize / 2)) * 0.25f,
									(ds[x][y] / 5) * 1.0f,
									(y - (gridSize / 2)) * 0.25f),
						Vector3.Zero, Vector2.Zero);
				}
			}

			int ctr = 0;
			indices = new int[(gridSize - 1) * (gridSize - 1) * 6];
			for (int y = 0; y < gridSize - 1; y++)
			{
				for (int x = 0; x < gridSize - 1; x++)
				{
					// tl - - tr
					//  | \   |
					//  |   \ |
					// bl - - br

					int tl = x + (y) * gridSize;
					int tr = (x + 1) + (y) * gridSize;
					int bl = x + (y + 1) * gridSize;
					int br = (x + 1) + (y + 1) * gridSize;

					// indices for first tri
					indices[ctr++] = (int)tl;
					indices[ctr++] = (int)br;
					indices[ctr++] = (int)bl;

					// normal for first tri
					Vector3 leg0 = verts[tl].Position - verts[bl].Position;
					Vector3 leg1 = verts[tl].Position - verts[br].Position;
					Vector3 norm = Vector3.Cross(leg0, leg1);

					verts[tl].Normal += norm;
					verts[br].Normal += norm;
					verts[bl].Normal += norm;

					// indices for 2nd tri
					indices[ctr++] = (int)tl;
					indices[ctr++] = (int)tr;
					indices[ctr++] = (int)br;

					// normal for 2nd tri
					leg0 = verts[tl].Position - verts[br].Position;
					leg1 = verts[tl].Position - verts[tr].Position;
					norm = Vector3.Cross(leg0, leg1);

					verts[tl].Normal += norm;
					verts[tr].Normal += norm;
					verts[br].Normal += norm;
				}
			}

			// normalize the normals
			foreach (VertexPositionNormalTexture v in verts)
				v.Normal.Normalize();
		}

		private void InitShaders()
		{
			float tilt = MathHelper.ToRadians(0);
			worldMatrix = Matrix.CreateRotationX(tilt) * Matrix.CreateRotationY(tilt);
			viewMatrix = Matrix.CreateLookAt(new Vector3(0, 25, 60), Vector3.Zero, Vector3.Up);

			projectionMatrix = Matrix.CreatePerspectiveFieldOfView(
				MathHelper.ToRadians(45),  // 45 degree angle
				(float)GraphicsDevice.Viewport.Width /
				(float)GraphicsDevice.Viewport.Height,
				1.0f, 128.0f);

			basicEffect = new BasicEffect(graphics.GraphicsDevice);
			basicEffect.VertexColorEnabled = false;
			basicEffect.LightingEnabled = true;
			basicEffect.FogEnabled = true;
			//basicEffect.FogColor = new Vector3(0.3f, 0.3f, 0.3f);
			basicEffect.FogStart = 32.0f;
			basicEffect.FogEnd = 128;
			basicEffect.PreferPerPixelLighting = true;

			basicEffect.World = worldMatrix;
			//basicEffect.View = viewMatrix;
			float rads = MathHelper.ToRadians(rotation);
			basicEffect.View = Matrix.CreateRotationY(rads) * Matrix.CreateLookAt(new Vector3(0, 25, 60), Vector3.Zero, Vector3.Up);

			basicEffect.Projection = projectionMatrix;

			// primitive color
			//basicEffect.AmbientLightColor = new Vector3(0.1f, 0.1f, 0.1f);
			basicEffect.DirectionalLight0.Enabled = true;
			basicEffect.DirectionalLight0.Direction = new Vector3(0.0f, -1.0f, -1.0f);

			basicEffect.DirectionalLight0.DiffuseColor = Color.OliveDrab.ToVector3();
		}

		protected override void LoadContent()
		{
			spriteBatch = new SpriteBatch(GraphicsDevice);
		}

		protected override void UnloadContent()
		{
		}

		protected override void Update(GameTime gameTime)
		{
			if (GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed)
				this.Exit();

			/*
			 * Keys:
			 *   Up/Down  - Increase/decrase roughness
			 *   Spacebar - Enable/disable rotation
			 *   Enter    - Generate new terrain
			 */
			KeyboardState keyState = Keyboard.GetState();
			if (keyState.GetPressedKeys().Length > 1)
			{
				if (!keyDown)
				{
					if (keyState.IsKeyDown(Keys.Up))
						roughness++;
					else if (keyState.IsKeyDown(Keys.Down))
						roughness--;
					else if (keyState.IsKeyDown(Keys.Space))
						doRotate = !doRotate;
					else if (keyState.IsKeyDown(Keys.Enter))
						seed = random.Next(100);

					keyDown = true;
					GenerateTerrain();
				}
			}
			else keyDown = false;

			if (doRotate)
			{
				rotation += (float)gameTime.ElapsedGameTime.TotalMilliseconds / 40.0f;

				// prevent overflow
				if (rotation >= 360)
					rotation -= 360;

				float rads = MathHelper.ToRadians(rotation);
				basicEffect.View = Matrix.CreateRotationY(rads) *Matrix.CreateLookAt(new Vector3(0, 25, 60),
					Vector3.Zero, Vector3.Up);
			}

			base.Update(gameTime);
		}

		protected override void Draw(GameTime gameTime)
		{
			GraphicsDevice.Clear(Color.SteelBlue);

			foreach (EffectPass pass in basicEffect.CurrentTechnique.Passes)
			{
				pass.Apply();

				GraphicsDevice.RasterizerState = rasterState;
				GraphicsDevice.DrawUserIndexedPrimitives<VertexPositionNormalTexture>(
					PrimitiveType.TriangleList,
					verts,
					0,
					verts.Length,
					indices,
					0,
					indices.Length / 3);
			}

			base.Draw(gameTime);
		}
	}
}

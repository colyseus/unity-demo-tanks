// 
// THIS FILE HAS BEEN GENERATED AUTOMATICALLY
// DO NOT CHANGE IT MANUALLY UNLESS YOU KNOW WHAT YOU'RE DOING
// 
// GENERATED USING @colyseus/schema 1.0.16
// 

using Colyseus.Schema;

namespace Tanks {
	public partial class TanksState : Schema {
		[Type(0, "array", typeof(ArraySchema<Player>))]
		public ArraySchema<Player> players = new ArraySchema<Player>();

		[Type(1, "array", typeof(ArraySchema<Weapon>))]
		public ArraySchema<Weapon> weapons = new ArraySchema<Weapon>();

		[Type(2, "ref", typeof(World))]
		public World world = new World();

		[Type(3, "array", typeof(ArraySchema<Projectile>))]
		public ArraySchema<Projectile> projectiles = new ArraySchema<Projectile>();

		[Type(4, "string")]
		public string gameState = default(string);

		[Type(5, "string")]
		public string previousGameState = default(string);

		[Type(6, "number")]
		public float currentTurn = default(float);

		[Type(7, "number")]
		public float turnNumber = default(float);

		[Type(8, "string")]
		public string statusMessage = default(string);
	}
}

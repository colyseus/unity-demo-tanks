// 
// THIS FILE HAS BEEN GENERATED AUTOMATICALLY
// DO NOT CHANGE IT MANUALLY UNLESS YOU KNOW WHAT YOU'RE DOING
// 
// GENERATED USING @colyseus/schema 1.0.16
// 

using Colyseus.Schema;

namespace Tanks {
	public partial class TanksState : Schema {
		[Type(0, "ref", typeof(GameRulesSchema))]
		public GameRulesSchema gameRules = new GameRulesSchema();

		[Type(1, "array", typeof(ArraySchema<Player>))]
		public ArraySchema<Player> players = new ArraySchema<Player>();

		[Type(2, "array", typeof(ArraySchema<Weapon>))]
		public ArraySchema<Weapon> weapons = new ArraySchema<Weapon>();

		[Type(3, "ref", typeof(World))]
		public World world = new World();

		[Type(4, "map", typeof(MapSchema<Projectile>))]
		public MapSchema<Projectile> projectiles = new MapSchema<Projectile>();

		[Type(5, "string")]
		public string gameState = default(string);

		[Type(6, "string")]
		public string previousGameState = default(string);

		[Type(7, "number")]
		public float currentTurn = default(float);

		[Type(8, "number")]
		public float turnNumber = default(float);

		[Type(9, "string")]
		public string statusMessage = default(string);

		[Type(10, "boolean")]
		public bool inProcessOfQuitingGame = default(bool);
	}
}

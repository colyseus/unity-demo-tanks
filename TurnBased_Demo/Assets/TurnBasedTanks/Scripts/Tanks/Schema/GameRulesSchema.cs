// 
// THIS FILE HAS BEEN GENERATED AUTOMATICALLY
// DO NOT CHANGE IT MANUALLY UNLESS YOU KNOW WHAT YOU'RE DOING
// 
// GENERATED USING @colyseus/schema 1.0.16
// 

using Colyseus.Schema;

namespace Tanks {
	public partial class GameRulesSchema : Schema {
		[Type(0, "int32")]
		public int MaxActionPoints = default(int);

		[Type(1, "int32")]
		public int MovementActionPointCost = default(int);

		[Type(2, "int32")]
		public int FiringActionPointCost = default(int);

		[Type(3, "int32")]
		public int ProjectileSpeed = default(int);

		[Type(4, "int32")]
		public int MaxHitPoints = default(int);

		[Type(5, "int32")]
		public int MovementTime = default(int);
	}
}

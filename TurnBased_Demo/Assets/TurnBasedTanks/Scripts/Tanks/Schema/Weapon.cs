// 
// THIS FILE HAS BEEN GENERATED AUTOMATICALLY
// DO NOT CHANGE IT MANUALLY UNLESS YOU KNOW WHAT YOU'RE DOING
// 
// GENERATED USING @colyseus/schema 1.0.16
// 

using Colyseus.Schema;

namespace Tanks {
	public partial class Weapon : Schema {
		[Type(0, "string")]
		public string name = default(string);

		[Type(1, "number")]
		public float maxCharge = default(float);

		[Type(2, "number")]
		public float chargeTime = default(float);

		[Type(3, "number")]
		public float radius = default(float);

		[Type(4, "number")]
		public float impactDamage = default(float);

		[Type(5, "number")]
		public float index = default(float);
	}
}

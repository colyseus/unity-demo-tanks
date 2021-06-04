// 
// THIS FILE HAS BEEN GENERATED AUTOMATICALLY
// DO NOT CHANGE IT MANUALLY UNLESS YOU KNOW WHAT YOU'RE DOING
// 
// GENERATED USING @colyseus/schema 1.0.16
// 

using Colyseus.Schema;

namespace Tanks {
	public partial class Projectile : Schema {
		[Type(0, "string")]
		public string key = default(string);

		[Type(1, "ref", typeof(Vector2))]
		public Vector2 coords = new Vector2();
	}
}

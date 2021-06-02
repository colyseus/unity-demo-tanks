// 
// THIS FILE HAS BEEN GENERATED AUTOMATICALLY
// DO NOT CHANGE IT MANUALLY UNLESS YOU KNOW WHAT YOU'RE DOING
// 
// GENERATED USING @colyseus/schema 1.0.16
// 

using Colyseus.Schema;

namespace Tanks {
	public partial class World : Schema {
		[Type(0, "number")]
		public float width = default(float);

		[Type(1, "number")]
		public float height = default(float);

		[Type(2, "array", typeof(ArraySchema<float>), "number")]
		public ArraySchema<float> grid = new ArraySchema<float>();
	}
}

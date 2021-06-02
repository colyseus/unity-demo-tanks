// 
// THIS FILE HAS BEEN GENERATED AUTOMATICALLY
// DO NOT CHANGE IT MANUALLY UNLESS YOU KNOW WHAT YOU'RE DOING
// 
// GENERATED USING @colyseus/schema 1.0.16
// 

using Colyseus.Schema;

namespace Tanks {
	public partial class Player : Schema {
		[Type(0, "string")]
		public string sessionId = default(string);

		[Type(1, "string")]
		public string readyState = default(string);

		[Type(2, "number")]
		public float playerId = default(float);

		[Type(3, "string")]
		public string name = default(string);

		[Type(4, "number")]
		public float hp = default(float);

		[Type(5, "ref", typeof(Vector2))]
		public Vector2 coords = new Vector2();

		[Type(6, "number")]
		public float currentWeapon = default(float);

		[Type(7, "number")]
		public float currentMovement = default(float);

		[Type(8, "number")]
		public float currentActionPoints = default(float);

		[Type(9, "number")]
		public float timestamp = default(float);

		[Type(10, "boolean")]
		public bool connected = default(bool);
	}
}

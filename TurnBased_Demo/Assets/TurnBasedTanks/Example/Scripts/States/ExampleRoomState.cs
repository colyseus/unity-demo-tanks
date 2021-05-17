using Colyseus.Schema;
using Colyseus;

public class ExampleRoomState : ColyseusRoomState
{
	//[CSAType(0, "map", typeof(ColyseusMapSchema<ExampleNetworkedEntity>))]
	//public ColyseusMapSchema<ExampleNetworkedEntity> networkedEntities = new ColyseusMapSchema<ExampleNetworkedEntity>();
	[Type(0, "map", typeof(MapSchema<ExampleNetworkedUser>))]
	public MapSchema<ExampleNetworkedUser> networkedUsers = new MapSchema<ExampleNetworkedUser>();
	[Type(1, "map", typeof(MapSchema<string>), "string")]
	public MapSchema<string> attributes = new MapSchema<string>();

}


"use strict";
var __decorate = (this && this.__decorate) || function (decorators, target, key, desc) {
    var c = arguments.length, r = c < 3 ? target : desc === null ? desc = Object.getOwnPropertyDescriptor(target, key) : desc, d;
    if (typeof Reflect === "object" && typeof Reflect.decorate === "function") r = Reflect.decorate(decorators, target, key, desc);
    else for (var i = decorators.length - 1; i >= 0; i--) if (d = decorators[i]) r = (c < 3 ? d(r) : c > 3 ? d(target, key, r) : d(target, key)) || r;
    return c > 3 && r && Object.defineProperty(target, key, r), r;
};
Object.defineProperty(exports, "__esModule", { value: true });
exports.ColyseusRoomState = exports.ColyseusNetworkedUser = void 0;
const schema_1 = require("@colyseus/schema");
// export class ColyseusNetworkedEntity extends Schema {
//     @type("string") id: string;
//     @type("string") ownerId: string;
//     @type("string") creationId: string = "";
//     @type("number") xPos: number = 0;
//     @type("number") yPos: number = 0;
//     @type("number") zPos: number = 0;
//     @type("number") xRot: number = 0;
//     @type("number") yRot: number = 0;
//     @type("number") zRot: number = 0;
//     @type("number") wRot: number = 0;
//     @type("number") xScale: number = 1;
//     @type("number") yScale: number = 1;
//     @type("number") zScale: number = 1;
//     @type("number") xVel: number = 0;
//     @type("number") yVel: number = 0;
//     @type("number") zVel: number = 0;
//     @type("number") timestamp: number;
//     @type({map: "string"}) attributes = new MapSchema<string>();
// }
class ColyseusNetworkedUser extends schema_1.Schema {
    constructor() {
        super(...arguments);
        this.attributes = new schema_1.MapSchema();
    }
}
__decorate([
    schema_1.type("string")
], ColyseusNetworkedUser.prototype, "id", void 0);
__decorate([
    schema_1.type("string")
], ColyseusNetworkedUser.prototype, "sessionId", void 0);
__decorate([
    schema_1.type("boolean")
], ColyseusNetworkedUser.prototype, "connected", void 0);
__decorate([
    schema_1.type("number")
], ColyseusNetworkedUser.prototype, "timestamp", void 0);
__decorate([
    schema_1.type({ map: "string" })
], ColyseusNetworkedUser.prototype, "attributes", void 0);
exports.ColyseusNetworkedUser = ColyseusNetworkedUser;
class ColyseusRoomState extends schema_1.Schema {
    constructor() {
        super(...arguments);
        // @type({ map: ColyseusNetworkedEntity }) networkedEntities = new MapSchema<ColyseusNetworkedEntity>();
        this.networkedUsers = new schema_1.MapSchema();
        this.attributes = new schema_1.MapSchema();
    }
}
__decorate([
    schema_1.type({ map: ColyseusNetworkedUser })
], ColyseusRoomState.prototype, "networkedUsers", void 0);
__decorate([
    schema_1.type({ map: "string" })
], ColyseusRoomState.prototype, "attributes", void 0);
exports.ColyseusRoomState = ColyseusRoomState;

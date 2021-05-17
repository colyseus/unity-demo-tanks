"use strict";
Object.defineProperty(exports, "__esModule", { value: true });
exports.TurnContainer = void 0;
const gameRules_1 = require("./gameRules");
class TurnContainer {
    get turnNumber() {
        return this._turnNumber;
    }
    get playerTurn() {
        return this._turnNumber % 2 == 0 ? 0 : 1; //Player 0 goes on even turns, will need to change this stuff if we ever want more than 2 players   
    }
    get currentMovement() {
        return this._currentMovement;
    }
    get currentAP() {
        return this._currentAP;
    }
    incrementTurnCount() {
        this._turnNumber++;
    }
    updateCurrentMovement(delta) {
        this._currentMovement += delta;
    }
    updateCurrentAP(delta) {
        this._currentAP += delta;
    }
    resetActions() {
        this._currentMovement = 0;
        this._currentAP = gameRules_1.GameRules.MaxAP;
    }
    completeReset() {
        this._turnNumber = 0;
        this.resetActions();
    }
}
exports.TurnContainer = TurnContainer;

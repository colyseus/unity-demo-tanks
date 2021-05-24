import { GameRules } from "./gameRules";

export class TurnContainer {
    private _turnNumber: number; // Tracker for the total number of turns in this game
    private _currentMovement: number; // Tracker for the number of times a player has moved this turn
    private _currentAP: number; // Tracker for the remaining AP the current player has ACTION POINT

    public get turnNumber(): number {
        return this._turnNumber;
    }

    public get playerTurn(): number {
        return this._turnNumber % 2 == 0 ? 0 : 1;   //Player 0 goes on even turns, will need to change this stuff if we ever want more than 2 players   
    }

    public get currentMovement(): number {
        return this._currentMovement;
    }

    public get currentAP(): number {
        return this._currentAP;
    }

    public incrementTurnCount() {
        this._turnNumber++;
    }

    public updateCurrentMovement(delta: number) {
        this._currentMovement += delta;
    }

    public updateCurrentAP(delta: number) {
        this._currentAP += delta;
    }

    public resetActions() {
        this._currentMovement = 0;
        this._currentAP = GameRules.MaxAP;
    }

    public completeReset() {
        this._turnNumber = 0;

        this.resetActions();
    }
}
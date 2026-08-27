import { $typeof } from "puerts";

class Rotate {
    constructor(bindTo) {
        this.bindTo = bindTo;
        this.bp = {};
        const ui = bindTo.GetComponent($typeof(CS.SOC.GamePlay.TSUIBinder));
        if (ui != null) {
            ui.InitRegisterControls(this);
            console.log("[rotate] bp keys=" + Object.keys(this.bp).join(","));
        }
        bindTo.JsUpdate = () => this.onUpdate();
        bindTo.JsOnDestroy = () => this.onDestroy();
    }

    onUpdate() {
        const r = CS.UnityEngine.Vector3.op_Multiply(
            CS.UnityEngine.Vector3.up,
            CS.UnityEngine.Time.deltaTime * 10
        );
        this.bindTo.transform.Rotate(r);
    }

    onDestroy() {
        console.log("[rotate] onDestroy");
        this.bindTo.JsUpdate = undefined;
        this.bindTo.JsOnDestroy = undefined;
    }
}

export function init(bindTo) {
    new Rotate(bindTo);
}

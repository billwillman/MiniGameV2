import { $typeof } from "puerts";

class Rotate {
    constructor(bindTo) {
        this.bindTo = bindTo;
        const uiType = $typeof(CS.SOC.GamePlay.TSUIBinder);
        const ui = bindTo.GetComponent(uiType);
        if (ui != null && ui.m_BindControls != null) {
            console.log("[rotate] TSUIBinder m_BindControls length=" + ui.m_BindControls.Length);
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

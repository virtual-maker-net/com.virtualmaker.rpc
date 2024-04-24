// Licensed under the MIT License. See LICENSE in the project root for license information.

import { writable } from "svelte/store";

interface UnityRpcMessage {
    event: string;
    args: any[];
    'rpc-id'?: number;
    'response-id'?: number;
}

class UnityRpc {
    private rpcId = 0;
    private eventHandlers: { [event: string]: ((...args: any[]) => any)[] } = {};
    private rpcs: { [rpcId: number]: (returnValue: any) => void } = {};
    private window: Window | null = null;

    public started = writable(false);

    public setUnityWindow(window: Window) {
        this.window = window;
        this.rpcId = 1;
        this.eventHandlers = {};
        this.rpcs = {};
        this.window.addEventListener('merciv-unity-to-js', this.onUnityMessage.bind(this) as EventListener);
        this.started.set(true);
    }

    public async call<T>(event: string, ...args: any[]): Promise<T> {
        return new Promise<T>(resolve => {
            const rpcId = this.rpcId++;
            this.rpcs[rpcId] = resolve;
            this.send({ event, args, 'rpc-id': rpcId });
        });
    }

    public raiseEvent(event: string, ...args: any[]) {
        this.send({ event, args });
    }

    public subscribe(event: string, handler: (...args: any[]) => any) {
        if (!this.eventHandlers[event]) {
            this.eventHandlers[event] = [];
        }

        this.eventHandlers[event].push(handler);
    }

    public unsubscribe = (event: string, handler: (...args: any[]) => any) => {
        this.eventHandlers[event] = this.eventHandlers[event].filter(h => h !== handler);
    }

    public createRpc<T>(event: string, handler: (...args: any[]) => any) {
        this.subscribe(event, handler);
    }

    public removeRpc = (event: string, handler: (...args: any[]) => any) => {
        this.unsubscribe(event, handler);
    }

    private send(message: UnityRpcMessage) {
        const event = new CustomEvent('merciv-js-to-unity', { detail: JSON.stringify(message) });
        this.window?.dispatchEvent(event);
    }

    private onUnityMessage(event: CustomEvent<any>) { // Update the type of the event parameter
        const customEvent = event as CustomEvent<any>;

        const message = JSON.parse(customEvent.detail);

        if (message['response-id']) {
            const responseId = message['response-id'];
            const response = this.rpcs[responseId];
            if (!response) {
                return;
            }

            response(message.args[0]);
            delete this.rpcs[responseId];
        } else {
            if (!this.eventHandlers[message.event]) {
                return;
            }

            for (const handler of this.eventHandlers[message.event]) {
                const returnValue = handler(...message.args);
                if (message['rpc-id']) {
                    this.send({ event: message.event, args: [returnValue], 'response-id': message['rpc-id'] });
                }
            }
        }
    }
}

const unity = new UnityRpc();
export default unity;
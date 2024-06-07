if param.D == 0 && (global.extr_key_0 != "" || global.extr_key_1 != "")
	M108	; Togli attesa riscaldamenti
	M25		; Pausa
	M0		; stampa
	M98 P"0:/gcodes/queue/inner/pause.g"

if param.D == 1 && (global.extr_key_2 != "" || global.extr_key_3 != "")
	M108	; Togli attesa riscaldamenti
	M25		; Pausa
	M0		; Stop stampa
	M98 P"0:/gcodes/queue/inner/pause.g"

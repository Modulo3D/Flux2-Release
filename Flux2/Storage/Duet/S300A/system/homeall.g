; homeall.g
; called to home all axes

; check no tool
M400
M577 P0 S1

; set tool
T-1 P0

;M98 P"homeb.g"			; Home B (Bellows)

;M98 P"homeu.g"			; Home U (Unloader)

M98 P"homec.g"			; Home C (ToolHead)

M98 P"homey.g"			; Home Y

M98 P"homex.g"			; Home X

M98 P"homez.g"			; Home Z
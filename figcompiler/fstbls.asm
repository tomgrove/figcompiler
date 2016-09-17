
DR				macro()

				ld a, (hl)
				ld (de), a
				inc de
				inc h

				mend

Char			macro()

				DR()
				DR()
				DR()
				DR()
				DR()
				DR()
				DR()
				DR()

				mend

                org $6000               ; Somewhere convenient

Start           pop de
				pop hl
				pop bc
Rows			Char()
				inc l
				Char()
				ld a,l
				add a,$1f
				ld l,a
				jr nc	L0
				ld a,h
				add a,8
				ld h,a
L0				dec b
				jr nz Rows
				jp  $6145

				

       output_bin "zt1.bin",$6000,$100    ; The binary file

/*
Zeus can print expressions during assembly. Useful for debugging sometimes...
*/
; zeusprint    "Hello! ",42
; zeusprinthex "Hello again! ",42

; You may want to drag the divider up to increase the size of the bottom window so
; you can see them...


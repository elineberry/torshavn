require util.fs
require random-util.fs

\ ### STATE ###
0 value turn
0 value forest-level
0 value dread 	\ How much the forest wants to kill you
0 value rogue.x
0 value rogue.y
8 value rogue.hp
100 value rogue.food
8 value rogue.strength
0 value forest-level-seed
false value is-playing?
false value do-turn?

\ ### STRUCTS ###
begin-structure unit
	field: u.x
	field: u.y
	field: u.color
	field: u.char
end-structure

\ ### CONSTANTS ###
80 constant map-width
24 constant map-height
map-width map-height * constant map-size
13 constant max-forest-level
here map-size allot constant map
here map-size allot constant visible
here 100 unit * allot constant units
char > constant c-exit

\ ### MOVEMENT ###
: validate-position
	rogue.x 0 max to rogue.x
	rogue.x map-width 1- min to rogue.x
	rogue.y 0 max to rogue.y
	rogue.y map-height 1- min to rogue.y ;
: handle-collision ;
: validate-move ( x-offset y-offset -- flag ) true to do-turn? 2drop true ;
: move-rogue.x { x-offset }
    rogue.x x-offset + to rogue.x ;
: move-rogue.y { y-offset }
    rogue.y y-offset + to rogue.y ;
: move-rogue { x-offset y-offset } 
    x-offset y-offset validate-move if
    x-offset move-rogue.x
    y-offset move-rogue.y 
		else handle-collision
		then validate-position ;
: d-left -1 0 ;
: d-right 1 0 ;
: d-up 0 -1 ;
: d-down 0 1 ;
: d-left-up -1 -1 ;
: d-left-down -1 1 ;
: d-right-up 1 -1 ;
: d-right-down 1 1 ;
: run-rogue ;		\ not implemented

\ ### MAP ###
: n-to-xy ( n -- x y ) map-width /mod ;
: .map map-size 0 do i map + c@ i n-to-xy at-xy emit loop ;
: .rogue [char] @ rogue.x rogue.y at-xy emit ;

\ ### PROCGEN FOREST ###
: random-char case
	1 of [char] { endof
	2 of [char] } endof
	3 of [char] ~ endof
	>r bl r> endcase ;
: set-forest-level-exit c-exit 0 map-size random-in-range map + c! ;
: new-forest-level 
	forest-level 1+ to forest-level 
	seed to forest-level-seed
	40 to rogue.x
	12 to rogue.y
	map-size 0 do 1d6 random-char map i + c! loop 
	set-forest-level-exit ;

\ ### STATUS LINE ###
: .status-line 
	0 map-height 1+ at-xy
	." turn: " turn 3 u.r .tab
	." lvl: " forest-level 3 u.r .tab
	." dread: " dread 3 u.r .tab
	." hp: " rogue.hp 3 u.r .tab
	." strength: " rogue.strength 3 u.r ;
: .debug-line
	0 map-height 2 + at-xy
	." location : " rogue.x 2 u.r ." ," rogue.y 2 u.r 
	.tab ." depth : " depth . ;

\ ### COMMANDS ###
: repeat-last-message ;
: show-help ;
: do-command
	key
		case
			[char] h of d-left move-rogue endof
			[char] j of d-down move-rogue endof
			[char] k of d-up move-rogue endof
			[char] l of d-right move-rogue endof
			[char] y of d-left-up move-rogue endof
			[char] u of d-right-up move-rogue endof
			[char] b of d-left-down move-rogue endof
			[char] n of d-right-down move-rogue endof
			[char] H of d-left run-rogue endof
			[char] L of d-right run-rogue endof
			[char] J of d-down run-rogue endof
			[char] K of d-up run-rogue endof
			[char] Y of d-left-up run-rogue endof
			[char] U of d-right-up run-rogue endof
			[char] B of d-left-down run-rogue endof
			[char] N of d-right-down run-rogue endof

			[char] M of repeat-last-message endof
			[char] q of s" Really quit?" toast [char] y <> to is-playing? endof
			[char] ? of show-help endof
		endcase
		;

\ ### GAME LOOP ###
: turn+ turn 1+ to turn ;
: increment-turn do-turn? if turn+ then ;
: update-fov ;
: update-ui .debug-line .status-line .map .rogue ;
: input-loop begin 10 ms key? until ;
: game-init page true to is-playing? new-forest-level ;
: game-loop
	hide-cursor
	util:set-colors
	game-init
	util:set-colors
	begin
		false to do-turn?
		util:set-colors
		update-fov
		update-ui
		input-loop
		do-command
		increment-turn
		is-playing? 0=
	until
  show-cursor
;

: start-new-game random-init game-loop page .debug-check-end ;
: str start-new-game ;

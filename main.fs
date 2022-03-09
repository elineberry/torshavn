require util.fs
require random-util.fs

\ ### DEFERS ###
defer 'post-move-actions

\ ### STRUCTS ###
begin-structure unit
	field: u.char
	field: u.color
	field: u.activated
end-structure
: units unit * ;

begin-structure item
	field: i.type
	field: i.char
	field: i.color
	field: i.food
	field: i.weapon
end-structure
: items item * ;

\ ### CONSTANTS ###
80 constant map-width
24 constant map-height
map-width map-height * constant map-size
13 constant max-forest-level
here map-size allot constant map
here map-size allot constant visible
here map-size units allot constant unit-array
here map-size items allot constant item-array
here map-size allot constant unit-move-queue
10 constant max-inventory
here max-inventory items allot constant inventory-array
char > constant c-forest-level-exit
char $ constant c-goal
125 constant c-tree1	\ {
123 constant c-tree2	\ } 
char ~ constant c-shrub
char # constant c-rock
5 constant food-velocity

\ ### STATE ###
0 value turn
0 value forest-level
0 value dread 	\ How much the forest wants to kill you
0 value rogue.x
0 value rogue.y
8 value rogue.max-hp
rogue.max-hp value rogue.hp
100 value rogue.food
8 value rogue.strength
0 value forest-level-seed
false value is-playing?
false value do-turn?

\ ### UI ###
: shw-msg ( n addr -- ) 1 map-height at-xy type ;
: clr-msg pad 80 bl fill pad 80 shw-msg ;
: add-msg clr-msg shw-msg ;

\ ### MOVEMENT ###
: n-to-xy ( n -- x y ) map-width /mod ;
: xy-to-n ( x y -- n ) map-width * + ;
: @unit ( n -- addr ) units unit-array + ;
: @item ( n -- addr ) items item-array + ;
: validate-position
	rogue.x 0 max to rogue.x
	rogue.x map-width 1- min to rogue.x
	rogue.y 0 max to rogue.y
	rogue.y map-height 1- min to rogue.y ;
: is-goal? ( n -- flag ) map + c@ c-goal = ;
: is-exit? ( n -- flag ) map + c@ c-forest-level-exit = ;
: attack-enemy ( n -- )
	@unit unit erase 
	s" You killed the fae!" add-msg ;
: is-enemy? ( n -- flag ) @unit @ 0> ;
: handle-collision ( n -- )
	dup is-enemy? if attack-enemy else drop then ;
: is-forest? map + c@ case
	c-tree1 of true endof
	c-tree2 of true endof
	c-shrub of true endof
	>r false r> endcase ;
: is-open-for-rogue? map + c@ case
	bl of true endof
	c-forest-level-exit of true endof
	c-goal of true endof
	>r false r> endcase ;
: chop-forest ( n -- ) dup map + c@ 
	c-shrub = if bl else c-shrub then
	swap map + c!
	dread 1+ to dread ;
: validate-move ( x-offset y-offset -- flag ) 
	rogue.y + swap rogue.x + swap xy-to-n
	dup is-enemy? if attack-enemy false else 
	dup is-forest? if chop-forest false else
	is-open-for-rogue? then then ;
: move-rogue.x { x-offset }
    rogue.x x-offset + to rogue.x ;
: move-rogue.y { y-offset }
    rogue.y y-offset + to rogue.y ;
: move-rogue { x-offset y-offset } 
		true to do-turn?
    x-offset y-offset validate-move if
    x-offset move-rogue.x
    y-offset move-rogue.y 
		 \ else handle-collision
		then validate-position 'post-move-actions ;
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
: rogue.n rogue.x rogue.y xy-to-n ;
: top ( n -- n ) map-width - -1 max ;
: right ( n -- n ) dup map-width mod
	map-width 1- = if drop -1 else
	1+ then ;
: left ( n -- n ) dup map-width mod 0 = 
	if drop -1 else 1- then ;
: bottom ( n -- n ) map-width + dup map-size > if drop -1 then ;
: top-right ( n -- n ) top right ;
: top-left ( n -- n ) top left ;
: bottom-right ( n -- n ) bottom right ;
: bottom-left ( n -- n ) bottom left ;
: is-adjacent? ( n1 n2 -- flag )
	2dup top = if 2drop true exit then
	2dup bottom = if 2drop true exit then
	2dup left = if 2drop true exit then
	2dup right = if 2drop true exit then
	2dup top-right = if 2drop true exit then
	2dup top-left = if 2drop true exit then
	2dup bottom-right = if 2drop true exit then
	bottom-left = if true exit then
	false ;
: .units map-size 0 do i @unit u.char c@
	dup 0> if i n-to-xy at-xy emit else drop then loop ;
: .items map-size 0 do i @item i.char c@ 
	dup 0> if i n-to-xy at-xy emit else drop then loop ;
: rock-color 35 escape-code ;
: color-for-char ( c -- ) case
	c-tree1 of tree-color endof
	c-tree2 of tree-color endof
	c-shrub of tree-color endof
	c-rock of rock-color endof
	\ c-forest-level-exit of 38 escape-code tty-bold endof
	>r tty-bold r> endcase ;
: .map map-size 0 do i map + c@ 
	dup color-for-char
	i n-to-xy at-xy emit tty-reset util:set-colors loop ;
: .rogue [char] @ rogue.x rogue.y at-xy emit ;

\ ### PROCGEN FOREST ###
: random-char case
	3 of c-tree1 endof
	4 of c-tree1 endof
	5 of c-shrub endof
	6 of c-rock endof	
	>r bl r> endcase ;
: exit-or-goal forest-level max-forest-level = if c-goal else c-forest-level-exit then ;
: set-forest-level-exit exit-or-goal 0 map-size random-in-range map + c! ;
: new-forest-level 
	forest-level 1+ to forest-level 
	seed to forest-level-seed
	40 to rogue.x
	12 to rogue.y
	map-size 0 do 3d6 random-char map i + c! loop 
	set-forest-level-exit ;

\ ### STATUS LINE ###
: .status-line 
	0 map-height 1+ at-xy
	." turn: " turn 3 u.r .tab
	." lvl: " forest-level 3 u.r .tab
	." dread: " dread 3 u.r .tab
	." hp: " rogue.hp 3 u.r ." (" rogue.max-hp . ." )" .tab
	." food: " rogue.food 4 u.r .tab
	." strength: " rogue.strength 3 u.r ;
: .debug-line
	0 map-height 2 + at-xy
	." location: " rogue.n 4 u.r ." :" rogue.x 2 u.r ." ," rogue.y 2 u.r 
	.tab ." here: " here 12 u.r
	.tab ." depth: " depth . ;

\ ### ITEMS ###
1 constant item-axe
2 constant item-mushroom
3 constant item-cudgel
4 constant item-bread
99 constant item-medicinedrug

: item$ ( n -- n addr )
	case
		item-axe of s" A woodman's axe" endof
		item-mushroom of s" mushroom" endof
		item-cudgel of s" cudgel" endof
		item-bread of s" a loaf of bread" endof 
		item-medicinedrug of s" The medicine drug" endof
	endcase ;

: make-item ( type char color food weapon "name" -- )
	create , , , , , ;
true false 0 char / item-axe make-item woodsman-axe
true false 0 char | item-cudgel make-item cudgel
false true 0 char % item-mushroom make-item wild-mushroom
false true 0 char % item-bread make-item bread
false false 0 char ! item-medicinedrug make-item medicinedrug


\ ### COMMANDS ###
: press-any-key-to-continue 10 map-height at-xy ." Press any key to continue"
	key drop page ;
: show-inventory
	page 10 2 at-xy ." Inventory " cr cr
	max-inventory 0 do 
		inventory-array i items + i.type @
		dup 0> if i . .tab item$ type cr else drop then 		
	loop press-any-key-to-continue ;
: repeat-last-message ;
: show-help ;
: drop-item ( n -- )
	items inventory-array + dup rogue.n @item brk item move brk item erase ;
: validate-inventory-selection ( n -- flag )
	items inventory-array + @ 0> ;
: validate-inventory-input ( n -- flag ) dup 0< swap 9 > or false = ;
: change-char-to-number ( char -- n ) [char] 0 - ;
: drop-item-command 
	s" Drop which item? 0-9" toast 
	change-char-to-number
	dup validate-inventory-input false = if drop s" cancelled" add-msg exit then
	dup validate-inventory-selection
	if drop-item s" Dropped" add-msg
	else drop s" No item in that slot" add-msg then ;
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

			[char] . of true to do-turn? endof
			[char] M of repeat-last-message endof
			[char] q of s" Really quit?" toast [char] y <> to is-playing? endof
			[char] ? of show-help endof
			[char] i of show-inventory endof
			[char] d of drop-item-command endof
		endcase
		;

\ ### GAME SETUP ###
: starting-inventory
	medicinedrug bread woodsman-axe 
	3 0 do i items inventory-array + item move loop ;
: find-empty-place-on-map ( -- n )
	begin
		0 map-size random-in-range dup map + c@
		bl = if true else drop false then
	until ;
	
: store-food-item! ( addr -- ) 
	dup i.char [char] % swap c!
	item-mushroom swap ! ;	
	
: populate-level-items 1d6 0 do 
		find-empty-place-on-map @item store-food-item! loop ;
: populate-level-units 1d6 0 do
		[char] f find-empty-place-on-map @unit u.char ! loop ;


\ ### GAME LOOP ###
: turn+ turn 1+ to turn ;
: increment-turn do-turn? if turn+ then ;
: update-fov ;
: update-ui .debug-line .status-line .map .items .units .rogue ;
: input-loop begin 10 ms key? until ;
: game-init starting-inventory page true to is-playing? 
	new-forest-level
	populate-level-units
	populate-level-items ;
: erase-level-items item-array map-size items erase ;
: erase-level-units unit-array map-size units erase ;
: erase-level-map map map-size erase ;
: erase-level 
	erase-level-items
	erase-level-units
	erase-level-map ;
: next-level 
	erase-level
	new-forest-level
	populate-level-units
	populate-level-items
	find-empty-place-on-map n-to-xy to rogue.y to rogue.x ;	
: declare-victory update-ui s" You win" toast drop false to is-playing? ;
: check-for-exit rogue.n is-exit?
	if next-level else rogue.n is-goal? if declare-victory then then ;
: empty-inventory-slot max-inventory 0 do inventory-array i items + @ 
	0= if inventory-array i items + unloop exit then loop false ;
: pick-up-item empty-inventory-slot dup false = 
	if drop s" No room for item." add-msg
	else item-array rogue.n items + tuck swap item move item erase then ;
: am-i-standing-on-something item-array rogue.n items + i.char c@ 0> 
	if pick-up-item then ; 
: is-dead? ( -- flag ) rogue.hp 0> false = ;
: process-death update-ui s" You died." toast drop false to is-playing? ;
: decrement-hp ( n -- ) rogue.hp swap - 0 max to rogue.hp ;
: decrement-food turn food-velocity mod 0= 
	if rogue.food 1- 0 max to rogue.food then ;
: starve rogue.food 0= if s" You're starving" add-msg 1 decrement-hp then ;
: populate-move-queue
	unit-move-queue map-size erase 
	map-size 0 do i @unit @ 0> i unit-move-queue + c! loop ;
: move-unit ( from-unit-no to-unit-no -- ) 
	@unit 					\ from-no to-addr
	over @unit			\ from-no to-addr from-addr
	swap						\ from-no from-addr to-addr
	unit move
	@unit unit erase ;
: direction-of-rogue ( n -- x y )
	n-to-xy rogue.y > if -1 else 1 then
	swap rogue.x > if -1 else 1 then swap ;
: enemy-destination-square ( n -- n )
	dup direction-of-rogue map-width * + + ;
: attack-rogue
	1d6 1 = if s" The fae hits!" add-msg 1d6 decrement-hp 
	else s" Miss" add-msg then ;
: enemy-attacks ( n -- flag )
	rogue.n is-adjacent? dup if attack-rogue then ;
: is-empty-square? ( n -- flag )
	dup rogue.n <>
	swap @unit @ 0= and ;
: enemy-movement
	populate-move-queue 
	map-size 0 do
		unit-move-queue i + c@ if 
		i enemy-attacks 0= if
		i i enemy-destination-square 
		dup is-empty-square? if
		move-unit else 2drop then then then loop ;
: post-move-actions 
		am-i-standing-on-something
		check-for-exit ;
' post-move-actions is 'post-move-actions
: post-turn-actions 
		clr-msg
		enemy-movement
		decrement-food
		starve
		increment-turn ;
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
		\ post-move-actions
		do-turn? if post-turn-actions then
		is-dead? if process-death then
		is-playing? 0=
	until
  show-cursor ;

: start-new-game random-init game-loop page .debug-check-end ;
: str start-new-game ;

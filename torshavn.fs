require util.fs
require random-util.fs
require messages.fs

\ ### DEFERS ###
defer 'post-move-actions
defer 'find-empty-place-on-map ( -- n )
defer 'fov-circle
defer 'is-empty-square?
defer 'activate-unit!

\ ### STRUCTS ###
begin-structure unit
	field: u.char
	field: u.color
	field: u.activated
	field: u.attack
	field: u.damage
	field: u.hp
end-structure
: units unit * ;

begin-structure item
	field: i.char
	field: i.color
	field: i.type
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
here map-size allot constant fov
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
10 constant dread-velocity

\ ### ITEM TYPES ###
1 constant item-axe
2 constant item-mushroom
3 constant item-cudgel
4 constant item-bread
99 constant item-medicinedrug


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
7 value fov-distance
false value is-playing?
false value do-turn?
false value wizard?

\ ### UI ###
: .message-line-clear pad 80 bl fill pad 80 0 map-height at-xy type ;
: .message-line .message-line-clear 0 map-height at-xy msg:show ;
: add-msg ( addr u -- ) msg:add .message-line ;

\ ### ARRAY ACCESS ###
: show-everything? wizard? forest-level 0= or
	forest-level max-forest-level = or ;
: bounds-check ( n -- n ) dup map-size > if abort" Out of bounds." then ;
: is-visible! ( n -- ) bounds-check visible + true swap c! ;
: is-fov! ( n -- ) bounds-check fov + true swap c! ;
: map! ( c n -- ) bounds-check map + c! ;

: n-to-xy ( n -- x y ) bounds-check map-width /mod ;
: xy-to-n ( x y -- n ) map-width * + bounds-check ;
: @unit ( n -- addr ) bounds-check units unit-array + ;
: @item ( n -- addr ) bounds-check items item-array + ;
: @inventory ( n -- addr ) items inventory-array + ;
: @item-type ( n - n ) @item i.type @ ;
: @inventory-type ( n - n ) @inventory i.type @ ;
: @unit-is-activated? ( n -- flag ) @unit u.activated c@ ;
: deactivate-unit! ( n -- ) @unit u.activated false swap c! ;

\ ### MOVEMENT ###
: validate-x ( x -- flag ) dup 0 >= swap map-width < and ;
: validate-y ( y -- flag ) dup 0 >= swap map-height < and ;
: validate-xy ( x y -- flag ) validate-y swap validate-x and ;
: validate-position
	rogue.x 0 max to rogue.x
	rogue.x map-width 1- min to rogue.x
	rogue.y 0 max to rogue.y
	rogue.y map-height 1- min to rogue.y ;
: is-goal? ( n -- flag ) map + c@ c-goal = ;
: is-exit? ( n -- flag ) map + c@ c-forest-level-exit = ;
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
: darken-forest dread dread-velocity mod 0=
	if 
		fov-distance 1 - 1 max to fov-distance 
		s" The forest grows more foreboding... " add-msg
	then ;
: increment-dread
	dread 1+ to dread 
	darken-forest ;
: am-i-weilding-an-axe? ( -- flag ) max-inventory 0 do 
	i items inventory-array + i.type @ item-axe =
	if true unloop exit then loop false ;
: chop-forest ( n -- ) 
	am-i-weilding-an-axe? false = if drop
	s" You need an axe to cut trees. " add-msg exit then
	dup map + c@ 
	c-shrub = if bl else c-shrub then
	swap map + c!
	increment-dread ;
: damage-enemy ( n -- )
	@unit unit erase 
	s" You killed the fae creature! " add-msg ;
: is-enemy? ( n -- flag ) @unit @ 0> ;
: to-hit-chance ( --n ) 90 dread - 15 max ;
: attack-enemy ( n -- )
	s" You swing at the creature with your axe... " add-msg
 	to-hit-chance percent-chance
	if damage-enemy else 'activate-unit! s" miss. " add-msg then increment-dread ;
: validate-move ( x-offset y-offset -- flag ) 
	rogue.y + swap rogue.x + swap 2dup validate-xy false =
	if 2drop false exit then
	xy-to-n
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
		'post-move-actions
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
: is-in-fov? ( n -- ) fov + c@ ;
: is-visible? ( n -- flag ) visible + c@ show-everything? or ;
: reset-colors tty-reset util:set-colors ;
: bright-white-color 97 escape-code ;
: bright-green-color 92 escape-code ;
: bright-gray-color 90 escape-code ;
: bright-magenta-color 95 escape-code ;
: bright-cyan-color 96 escape-code ;
: activated-enemy-color bright-green-color ;
: rock-color 35 escape-code ;
: item-color bright-cyan-color ;
: enemy-color 37 escape-code ;
: rogue-color bright-white-color ;
: .units map-size 0 do i @unit u.char c@
	dup 0>
	if i is-in-fov?
	if i @unit-is-activated? if activated-enemy-color else enemy-color then 
	i n-to-xy at-xy emit
	else drop then
	else drop then
	loop reset-colors ;
: .items
	item-color
	map-size 0 do i @item i.char c@ 
	dup 0> if
	i is-in-fov? if 	
	i n-to-xy at-xy emit else drop then else drop then loop reset-colors ;
: color-for-char ( c -- ) case
	c-tree1 of tree-color endof
	c-tree2 of tree-color endof
	c-shrub of shrub-color endof
	c-rock of rock-color endof
	\ bl of 45 escape-code endof
	\ c-forest-level-exit of 38 escape-code tty-bold endof
	>r tty-bold r> endcase ;
: .map map-size 0 do 
	i is-visible? if	
	i map + c@ else bl then 
	i is-in-fov? if dup color-for-char then
	i n-to-xy at-xy emit reset-colors loop ;
: .rogue rogue-color [char] @ rogue.x rogue.y at-xy emit reset-colors ;

\ ### FOV ###
: .draw-a-circle { radius -- }
	rogue.y radius + rogue.y radius - brk do 
		i rogue.y - brk \ int dy = y - center.y
		sq radius sq - brk sqrt \ float dx  = sqrt(radius*radius - dy*dy);
		dup rogue.x swap +
		swap rogue.x - brk
	\	do i j at-xy [char] X emit loop
	loop ;

\ ### FOV 2 ###
5 constant fov-step
fov-step -1 * constant -fov-step
100 fov-step / constant slope-m
: add-pt-to-fov ( x y -- ) xy-to-n is-fov! ;
: add-pt-to-visible ( x y -- ) xy-to-n is-visible! ;
: blocks-fov? ( x y -- ) xy-to-n map + c@ bl false = ;
: sloped-line { x y x-slope y-slope distance }
  distance 0 do
	i x-slope * 100 / x +
	i y-slope * 100 / y +
	2dup validate-xy false = if 2drop unloop exit then
	2dup add-pt-to-fov
	add-pt-to-visible \ TODO check for blocks and edge of board
	\ blocks-fov? if unloop exit then 
  loop ;
: calculate-fov
  fov-step -fov-step do fov-step -fov-step do 
		rogue.x rogue.y i slope-m * j slope-m * fov-distance sloped-line
	loop loop ;
: update-fov
	fov map-size erase
	rogue.x rogue.y fov-distance 'fov-circle ;

\ ### PROCGEN FOREST ###
: random-char case
	3 of c-rock endof
	4 of c-rock endof
	5 of c-shrub endof
	6 of c-shrub endof	
	7 of c-tree1 endof
	8 of c-tree1 endof
	9 of c-tree1 endof
	10 of c-tree1 endof
	11 of c-tree2 endof
	12 of c-tree2 endof
	13 of c-tree2 endof
	14 of c-tree2 endof
	15 of c-shrub endof
	16 of c-shrub endof
	17 of c-rock endof
	18 of c-rock endof
	endcase ;
: exit-or-goal forest-level max-forest-level = if c-goal else c-forest-level-exit then ;
: set-forest-level-exit exit-or-goal 'find-empty-place-on-map map + c! ;
: put-trees-in-forest map-size 0 do 3d6 random-char map i + c! loop ;
: put-bl-on-map map map-size bl fill ;
: dig-floor-validate ( x y -- flag )
	map-height >= swap map-width >= or ;
: dig-floor ( x y -- )
	2dup dig-floor-validate if 2drop exit then
	75 percent-chance if 
	xy-to-n bl swap map! else 2drop then ;
: dig-room { x y width height }
	height y + y do
	    width x + x do
			i j dig-floor
		loop
	loop ;
: dig-random-room
		0 map-width 10 - random-in-range
		0 map-height 3 - random-in-range
		1d6 3 +
		2 3 random-in-range
		dig-room ;
: dx-dy { x1 y1 x2 y2 -- dx dy }
	x1 x2 max x1 x2 min -
	y1 y2 max y1 y2 min - ;	
: angled-distance { dx dy -- n }
	dx sq dy sq + sqrt ;
: point-angled-distance dx-dy angled-distance ;
: is-fov-visible! ( n -- ) dup is-visible! is-fov! ;
: fov-circle { x y r -- }
	\ move to top right of square
	\ loop through square
	\ if distance is less than radius
	\ sqrt( dx*dx + dy*dy )
	\ draw or something		
	map-size 0 do 
		x y i n-to-xy point-angled-distance
		r <
		if i is-fov-visible! then 
	loop ;
' fov-circle is 'fov-circle
: empty-spaces-on-map ( -- n )
	0 map-size 0 do map i + c@ bl = if 1+ then loop ;
: make-clearings-in-forest
	10 10 10 5 dig-room
	begin
	\	0 map-size random-in-range n-to-xy 1d6 3 + 1d6 1 + dig-room 	
		dig-random-room
		empty-spaces-on-map map-size 2 / > 
	until ;	
: put-rogue-on-map 'find-empty-place-on-map n-to-xy to rogue.y to rogue.x ;
: new-forest-level 
	forest-level 1+ to forest-level 
	seed to forest-level-seed
	put-trees-in-forest
	make-clearings-in-forest
	set-forest-level-exit
	put-rogue-on-map ;

\ ### STATUS LINE ###
: status-line-y map-height 1+ ;
: .status-line-bg pad 80 bl fill 0 status-line-y at-xy pad 80 type ;
: .status-line 
	tty-inverse
	.status-line-bg
	0 map-height 1+ at-xy
	." turn: " turn 3 u.r .tab
	." lvl: " forest-level 3 u.r .tab
	." dread: " dread 3 u.r .tab
	." hp: " rogue.hp 3 u.r .tab
	." food: " rogue.food 4 u.r .tab
	." str: " rogue.strength 3 u.r tty-reset util:set-colors ;
: .debug-line
	0 map-height 2 + at-xy
	." location: " rogue.n 4 u.r ." :" rogue.x 2 u.r ." ," rogue.y 2 u.r 
	.tab ." here: " here 12 u.r
	.tab ." depth: " depth . ;

\ ### ITEMS ###
: item$ ( n -- n addr )
	case
		item-axe of s" woodman's axe" endof
		item-mushroom of s" wild mushroom" endof
		item-cudgel of s" cudgel" endof
		item-bread of s" a loaf of bread" endof 
		item-medicinedrug of s" medicine drug" endof
	endcase ;

: make-item ( type char color food weapon "name" -- )
	create , , , , , ;
true false item-axe 0 char /  make-item woodsman-axe
true false item-cudgel 0 char | make-item cudgel
false true item-mushroom 0 char %  make-item wild-mushroom
false true item-bread 0 char %  make-item bread
false false item-medicinedrug 0 char ! make-item medicinedrug

: make-unit ( hp damage attack activated color char "name" -- )
	create , , , , , , ;
1 1 1 false 0 char c make-unit unit-chipmunk
1 1 5 false 0 char f make-unit unit-fae


\ ### COMMANDS ###
: centered-x ( u -- x ) map-width swap - 2 / ;
: .centered ( addr u -- ) dup centered-x 1 at-xy type ;
: .formatted-title ( addr u -- )
	tty-inverse .centered reset-colors ;
: .press-key-prompt s" -- press any key to continue --"
	dup centered-x map-height at-xy type key drop ;
: title$ s" Torshavn: The Fae Forest" ;
: version$ s" 1.0" ;
: show-help
	page title$ .formatted-title space version$ type
	CR CR
	."    Map Items " cr cr
	." letter -- enemy" cr
	." }{~    -- forest" cr
	." %      -- wild mushroom" cr
	." >      -- level exit" cr
	." $      -- final exit" cr
	cr
	."   Commands " cr cr
	." hjkl -- movement"  CR
	." yubn -- diagonal movement"  CR
	CR
	." ?    -- help (this screen)" cr
	." i    -- show inventory" cr
	." e    -- eat a mushroom" cr
	." d    -- drop an item" cr
	." m    -- message list" CR
	." s    -- show story" cr
	." q    -- quit game" CR
	cr .press-key-prompt ;
: show-story
	page title$ .formatted-title space version$ type
	cr cr
	." King Torshavn is dying." cr
	cr
	." A sickness fouls his blood and he grows weaker every day. " cr
	." You must carry the life saving medicinedrug through the " cr
	." fae forest and deliver it to the palace before the king" cr
	." perishes and the land is plunged into chaos or democracy." cr
	cr
	." Journey lightly and swift. Do not disturb the peace of the" cr
	." forest or the creatures within!" cr
	cr
	." Wild mushrooms will sustain you and draw creatures away from" cr
	." you when dropped. Use them wisely!"
	cr .press-key-prompt ;
: show-inventory
	page s" Inventory" .formatted-title
	cr cr
	max-inventory 0 do 
		inventory-array i items + i.type @
		dup 0> if i . .tab item$ type cr else drop then 		
	loop
	cr cr .press-key-prompt page ;
: show-message-history
	page s" Message History" .formatted-title
	cr cr
	msg:show-full
	cr .press-key-prompt ;
: am-i-carrying-mushrooms? ( -- n ) max-inventory 0 do 
	i items inventory-array + i.type @ item-mushroom =
	if true unloop exit then loop false ;
: benefits-of-eating-mushroom rogue.hp 1d6 + to rogue.hp
	rogue.food 3d6 + to rogue.food ;
: eat-mushroom max-inventory 0 do
	i items inventory-array + i.type @ item-mushroom = 
	if i items inventory-array + item erase unloop exit then loop ;
: prompt-to-eat-mushroom s" Eat a wild mushroom?" toast [char] y = if
	eat-mushroom benefits-of-eating-mushroom then ;
: eat-something am-i-carrying-mushrooms?
	if prompt-to-eat-mushroom else
	s" You don't have any mushrooms. " add-msg then ;
: dropped-a-mushroom? ( -- flag )
	rogue.n @item-type item-mushroom = ;
: dropped-mushroom-effects
	dropped-a-mushroom? if
	s" You dropped a wild mushroom. " add-msg
	rogue.n @item item erase
	map-size 0 do 
		i is-in-fov?
		if i deactivate-unit! then 
	loop then ;
: drop-item ( n -- )
	items inventory-array + dup rogue.n @item item move item erase ;
: validate-inventory-selection ( n -- flag )
	items inventory-array + @ 0> ;
: validate-inventory-input ( n -- flag ) dup 0< swap 9 > or false = ;
: change-char-to-number ( char -- n ) [char] 0 - ;
: drop-item-command 
	s" Drop which item? 0-9" toast 
	change-char-to-number
	dup validate-inventory-input false = if drop s" canceled " add-msg exit then
	dup validate-inventory-selection
	if drop-item dropped-mushroom-effects s" Dropped. " add-msg
	else drop s" No item in that slot. " add-msg then ;
: do-command
	key
		msg:next-line \ This is where we clear the messages from last turn.
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
			[char] e of eat-something endof
			[char] m of show-message-history endof
			[char] q of s" Really quit?" toast [char] y <> to is-playing? endof
			[char] ? of show-help endof
			[char] i of show-inventory endof
			[char] d of drop-item-command endof
			[char] s of show-story endof
			[char] Z of debug? if wizard? 0= to wizard? then endof
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
' find-empty-place-on-map is 'find-empty-place-on-map
	
: store-food-item! ( addr -- ) 
	dup i.char [char] % swap c!
	item-mushroom swap ! ;	

: random-unit ( -- addr ) 1d6 2 > if unit-fae else unit-chipmunk then ;
: populate-level-items 1d6 0 do 
		wild-mushroom find-empty-place-on-map @item item move loop ;
: populate-level-units 3d6 0 do
	random-unit find-empty-place-on-map @unit unit move loop ;


\ ### GAME LOOP ###
: turn+ turn 1+ to turn ;
: increment-turn do-turn? if turn+ then ;
: update-ui .debug-line .status-line .message-line .map .items .units .rogue ;
: input-loop begin 10 ms key? until ;
: erase-level-items item-array map-size items erase ;
: erase-level-units unit-array map-size units erase ;
: erase-level-map map map-size erase ;
: erase-level 
	erase-level-items
	erase-level-units
	erase-level-map 
	fov map-size erase
	visible map-size erase ;

\ ### NEW LEVEL ###
: put-circle-clearing-in-forest { x y r -- }
	map-size 0 do 
		x y i n-to-xy point-angled-distance
		r <
		if i n-to-xy dig-floor then 
	loop ;
: clear-rogue-square bl rogue.n map! ;
: first-level
	seed to forest-level-seed
	put-trees-in-forest 
	40 12 10 put-circle-clearing-in-forest
	exit-or-goal 47 12 xy-to-n map!
	populate-level-items
	40 to rogue.x 12 to rogue.y 
	clear-rogue-square ;
: last-level
	first-level
	7 0 do i 5 * 8 14 random-in-range 3 put-circle-clearing-in-forest loop 
	0 to rogue.x 
	clear-rogue-square ;
: middle-level
	new-forest-level
	populate-level-units
	populate-level-items
	put-rogue-on-map ;
: next-level 
	page
	erase-level
	forest-level 0 = if first-level else
	forest-level max-forest-level = if last-level else
	middle-level then then ;
: game-init starting-inventory page true to is-playing? 
	next-level ;
: declare-victory update-ui s" You win" toast drop false to is-playing? ;
: increment-forest-level forest-level 1+ to forest-level ;
: check-for-exit rogue.n is-exit?
	if increment-forest-level next-level else
	rogue.n is-goal? if declare-victory then then ;
: empty-inventory-slot max-inventory 0 do inventory-array i items + @ 
	0= if inventory-array i items + unloop exit then loop false ;
: 'notify-pickup 
	pad 80 erase s" Picked up " pad swap move
	rogue.n @item i.char c@ item$ pad 10 + swap move
	pad 50 add-msg ;
: notify-pickup 
	s" Picked up " add-msg 
	rogue.n @item i.type @ item$ add-msg ;
: pick-up-item empty-inventory-slot dup false = 
	if drop s" No room for item. " add-msg
	else
	notify-pickup
	item-array rogue.n items + tuck swap item move item erase then ;
: am-i-standing-on-something item-array rogue.n items + i.char c@ 0> 
	if pick-up-item then ; 
: is-dead? ( -- flag ) rogue.hp 0> false = ;
: process-death update-ui s" You died." toast drop false to is-playing? ;
: decrement-hp ( n -- ) rogue.hp swap - 0 max to rogue.hp ;
: decrement-food turn food-velocity mod 0= 
	if rogue.food 1- 0 max to rogue.food then ;
: starve rogue.food 0= if s" You're starving. " add-msg 1 decrement-hp then ;
: has-unit? ( n -- flag ) @unit @ 0> ;
: activate-unit! ( n -- ) @unit u.activated true swap c! ;
' activate-unit! is 'activate-unit!
: activate-units-for-next-turn
	map-size 0 do 
		i has-unit? if
			dread percent-chance if 	
				i activate-unit! then
		then 
	loop ;
: unit-is-activated? ( n -- flag ) @unit u.activated c@ ;
: populate-move-queue
	unit-move-queue map-size erase 
	map-size 0 do i unit-is-activated? i unit-move-queue + c! loop ;
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
	s" The fae attacks... " add-msg
	1d6 1 = if s" The fae hits! " add-msg 1d6 decrement-hp 
	else s" and misses. "  add-msg then ;
: enemy-attacks ( n -- flag )
	rogue.n is-adjacent? dup if attack-rogue then ;
: is-empty-square? ( n -- flag )
	dup rogue.n <>
	swap @unit @ 0= and ;
' is-empty-square? is 'is-empty-square?
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
		enemy-movement
		activate-units-for-next-turn
		decrement-food
		starve
		increment-turn ;
: game-loop
	hide-cursor
	util:set-colors
	game-init
	util:set-colors
	show-story
	show-help
	begin
		false to do-turn?
		util:set-colors
		update-fov
		update-ui
		input-loop
		do-command
		do-turn? if post-turn-actions then
		is-dead? if process-death then
		is-playing? 0=
	until
  show-cursor ;

: start-new-game random-init game-loop page ;
: str start-new-game .debug-check-end ;

require ttester.fs
: brk CR ." :" .s key drop ;

\ ### CONSTANTS ###
80 constant msg:width
20 constant msg:height
msg:width msg:height * constant msg:buffer-size
here msg:buffer-size allot constant msg:buffer

\ ### VARIABLES ###
0 value msg:line
0 value msg:char

\ ### CALCULATED ###
: msg:lines ( n -- n ) msg:width * ;
: msg:line-start ( n -- addr ) msg:lines msg:buffer + ;
: msg:line-start-current ( -- addr ) msg:line msg:line-start ;
: msg:pointer msg:line-start-current msg:char + ;

\ ### DISPLAY ###
: msg:show msg:line-start-current msg:width type ;
: msg:show-full 
	msg:height 0 do 
		i msg:line + 1+ msg:height mod msg:line-start msg:width type cr
	loop ;

\ ### WRITE ###
: msg:erase-full msg:buffer msg:buffer-size erase ;
: msg:will-fit-on-line? ( n -- flag ) msg:char + msg:width > ;
: msg:next-line 
	msg:char 0= if exit then 		\ we are already on a new line
	msg:line 1+ msg:height mod to msg:line
	0 to msg:char
	msg:line-start-current msg:width erase ;
: msg:trim ( n -- n ) msg:width min ;
: msg:add ( addr n -- ) 
	msg:trim
	>r r@ msg:will-fit-on-line?  negate if msg:next-line then 
	 msg:pointer r@  move 
	msg:char r> + to msg:char ;

\ ### TESTS ###
T{ 0 to msg:line msg:pointer -> msg:buffer }T
T{ s" ZZZ" msg:add msg:char -> 3 }T
T{ msg:erase-full -> }T

T{ depth -> 0 }T

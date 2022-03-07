require random.fs

: random-in-range ( min max -- n )
	2dup > abort" min exceeds max"	
	over - 1+ random + ;
: random-init utime drop seed ! ;
: 1d6 ( -- n ) 6 random 1+ ;
: 2d6 ( -- n ) 1d6 1d6 + ;
: 3d6 ( -- b ) 1d6 1d6 1d6 + + ;

random-init

require ttester.fs
require main.fs

: test-adjacent 
	map-size 0 do map-size 0 do 
	." i: " i 5 u.r ."     j: " j 5 u.r ."     adjacent? "
	i j is-adjacent? 
	if ." YES" else ." NO" then cr
	loop loop ;
cr cr .( ### TESTS ### ) cr cr
T{ 0 value item1 -> }T
T{ new-forest-level find-empty-place-on-map map-size < -> true }T
T{ find-empty-place-on-map to item1 item1 map-size < -> true }T
T{ item1 @item store-food-item! -> }T
T{ item1 @item i.type @ -> item-mushroom }T
T{ item1 @item i.char c@ -> char % }T
T{ 2 3 is-adjacent? -> true }T
T{ 100 right -> 101 }T
T{ 100 top -> 20 }T
T{ 100 left -> 99 }T
T{ 0 top -> -1 }T
T{ 2 50 is-adjacent? -> false }T
T{ 100 bottom -> 180 }T
T{ 1000 99 is-adjacent? -> false }T
T{ 80 1 is-adjacent? -> false }T
T{ test-adjacent depth -> 0 }T

cr cr .( ### COMPLETE ### ) cr cr

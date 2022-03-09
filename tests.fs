require ttester.fs
require main.fs

cr cr .( ### TESTS ### ) cr cr
T{ 0 value item1 -> }T
T{ new-forest-level find-empty-place-on-map map-size < -> true }T
T{ find-empty-place-on-map to item1 item1 map-size < -> true }T
T{ item1 @item store-food-item! -> }T
T{ item1 @item i.type @ -> item-mushroom }T
T{ item1 @item i.char c@ -> char % }T


cr cr .( ### COMPLETE ### ) cr cr

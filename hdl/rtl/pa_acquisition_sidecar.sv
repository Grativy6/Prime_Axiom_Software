`default_nettype none

// COLD_MAG acquisition control: transparent truth-table realization of exact
// constant divisibility. Elaboration uses only constant arithmetic to choose
// equality minterms; the emitted circuit is exclusively pa_nand2-derived.

module pa_equal_constant #(
    parameter integer W = 4,
    parameter integer VALUE = 0
)(
    input  wire [W-1:0] a,
    output wire         equal
);
    wire [W-1:0] match;
    generate
        for (genvar i = 0; i < W; i = i + 1) begin : g_bit
            if (((VALUE >> i) & 1) != 0) begin : g_one
                assign match[i] = a[i];
            end else begin : g_zero
                pa_not u_not(.a(a[i]), .y(match[i]));
            end
        end
    endgenerate
    pa_reduce_and #(.W(W)) u_all(.a(match), .y(equal));
endmodule

module pa_divisible_constant #(
    parameter integer W = 4,
    parameter integer DIVISOR = 2
)(
    input  wire [W-1:0] magnitude,
    output wire         divisible
);
    localparam integer MAX_VALUE = (1 << W) - 1;
    localparam integer MATCH_COUNT = MAX_VALUE / DIVISOR;
    wire [MATCH_COUNT-1:0] match;
    generate
        for (genvar i = 0; i < MATCH_COUNT; i = i + 1) begin : g_match
            pa_equal_constant #(.W(W), .VALUE((i+1)*DIVISOR)) u_eq(
                .a(magnitude), .equal(match[i])
            );
        end
    endgenerate
    pa_reduce_or #(.W(MATCH_COUNT)) u_any(.a(match), .y(divisible));
endmodule

module pa_divide_constant_exact #(
    parameter integer W = 4,
    parameter integer DIVISOR = 2
)(
    input  wire [W-1:0] magnitude,
    output wire [W-1:0] quotient,
    output wire         exact
);
    localparam integer MAX_VALUE = (1 << W) - 1;
    localparam integer MATCH_COUNT = MAX_VALUE / DIVISOR;
    wire [MATCH_COUNT-1:0] match;
    wire [MATCH_COUNT-1:0] selected [0:W-1];
    generate
        for (genvar i = 0; i < MATCH_COUNT; i = i + 1) begin : g_match
            pa_equal_constant #(.W(W), .VALUE((i+1)*DIVISOR)) u_eq(
                .a(magnitude), .equal(match[i])
            );
            for (genvar bit_index = 0; bit_index < W; bit_index = bit_index + 1) begin : g_select
                if ((((i+1) >> bit_index) & 1) != 0) begin : g_used
                    assign selected[bit_index][i] = match[i];
                end else begin : g_unused
                    assign selected[bit_index][i] = 1'b0;
                end
            end
        end
        for (genvar bit_index = 0; bit_index < W; bit_index = bit_index + 1) begin : g_quotient
            pa_reduce_or #(.W(MATCH_COUNT)) u_or(.a(selected[bit_index]), .y(quotient[bit_index]));
        end
    endgenerate
    pa_reduce_or #(.W(MATCH_COUNT)) u_exact(.a(match), .y(exact));
endmodule

module pa_cold_encode_s4 #(
    parameter integer W = 4,
    parameter integer T2 = 3,
    parameter integer T3 = 2,
    parameter integer T5 = 1,
    parameter integer T7 = 1
)(
    input  wire [W-1:0]  magnitude,
    output wire          zero,
    output wire          valid,
    output wire [T2-1:0] t2,
    output wire [T3-1:0] t3,
    output wire [T5-1:0] t5,
    output wire [T7-1:0] t7
);
    function automatic integer ipow(input integer base, input integer exponent);
        integer value;
        begin
            value = 1;
            for (integer i = 0; i < exponent; i = i + 1) value = value * base;
            ipow = value;
        end
    endfunction
    (* pa_region = "control" *) pa_equal_constant #(.W(W),.VALUE(0)) u_zero(.a(magnitude),.equal(zero));
    // Every W-bit magnitude has an exact S4 sidecar.  For zero, every finite
    // threshold is true (v_p(0)=+infinity) and the explicit zero tag carries
    // the non-finite meaning.
    assign valid = 1'b1;
    generate
        for (genvar k2 = 0; k2 < T2; k2 = k2 + 1) begin : g_p2
            wire nonzero_divisible;
            (* pa_region = "lane2" *) pa_divisible_constant #(.W(W),.DIVISOR(ipow(2,k2+1))) u(.magnitude(magnitude),.divisible(nonzero_divisible));
            pa_or2 u_zero_is_divisible(.a(zero),.b(nonzero_divisible),.y(t2[k2]));
        end
        for (genvar k3 = 0; k3 < T3; k3 = k3 + 1) begin : g_p3
            wire nonzero_divisible;
            (* pa_region = "lane3" *) pa_divisible_constant #(.W(W),.DIVISOR(ipow(3,k3+1))) u(.magnitude(magnitude),.divisible(nonzero_divisible));
            pa_or2 u_zero_is_divisible(.a(zero),.b(nonzero_divisible),.y(t3[k3]));
        end
        for (genvar k5 = 0; k5 < T5; k5 = k5 + 1) begin : g_p5
            wire nonzero_divisible;
            (* pa_region = "lane5" *) pa_divisible_constant #(.W(W),.DIVISOR(ipow(5,k5+1))) u(.magnitude(magnitude),.divisible(nonzero_divisible));
            pa_or2 u_zero_is_divisible(.a(zero),.b(nonzero_divisible),.y(t5[k5]));
        end
        for (genvar k7 = 0; k7 < T7; k7 = k7 + 1) begin : g_p7
            wire nonzero_divisible;
            (* pa_region = "lane7" *) pa_divisible_constant #(.W(W),.DIVISOR(ipow(7,k7+1))) u(.magnitude(magnitude),.divisible(nonzero_divisible));
            pa_or2 u_zero_is_divisible(.a(zero),.b(nonzero_divisible),.y(t7[k7]));
        end
    endgenerate
endmodule

module pa_decode2(
    input wire [1:0] select,
    output wire [3:0] one_hot
);
    wire n0,n1;
    pa_not u_n0(.a(select[0]),.y(n0));
    pa_not u_n1(.a(select[1]),.y(n1));
    pa_and2 u_0(.a(n1),.b(n0),.y(one_hot[0]));
    pa_and2 u_1(.a(n1),.b(select[0]),.y(one_hot[1]));
    pa_and2 u_2(.a(select[1]),.b(n0),.y(one_hot[2]));
    pa_and2 u_3(.a(select[1]),.b(select[0]),.y(one_hot[3]));
endmodule

module pa_therm_increment_lane #(
    parameter integer T = 3
)(input wire [T-1:0] a, output wire [T-1:0] y, output wire saturated);
    assign y[0]=1'b1;
    generate for(genvar k=1;k<T;k=k+1) begin:g_shift assign y[k]=a[k-1]; end endgenerate
    assign saturated=a[T-1];
endmodule

module pa_therm_decrement_lane #(
    parameter integer T = 3
)(input wire [T-1:0] a, output wire [T-1:0] y);
    generate for(genvar k=0;k<T-1;k=k+1) begin:g_shift assign y[k]=a[k+1]; end endgenerate
    assign y[T-1]=1'b0;
endmodule

module pa_vsc_query_s4 #(
    parameter integer T2=3, parameter integer T3=2,
    parameter integer T5=1, parameter integer T7=1
)(
    input wire valid,
    input wire [T2-1:0] t2,input wire [T3-1:0] t3,
    input wire [T5-1:0] t5,input wire [T7-1:0] t7,
    input wire [1:0] prime_select,
    output wire predicate,
    output wire rejected
);
    wire low,high,selected;
    wire canonical;
    pa_therm_validate_s4 #(.T2(T2),.T3(T3),.T5(T5),.T7(T7)) check(.t2(t2),.t3(t3),.t5(t5),.t7(t7),.valid(canonical));
    pa_mux2 m0(.d0(t2[0]),.d1(t3[0]),.sel(prime_select[0]),.y(low));
    pa_mux2 m1(.d0(t5[0]),.d1(t7[0]),.sel(prime_select[0]),.y(high));
    pa_mux2 mp(.d0(low),.d1(high),.sel(prime_select[1]),.y(selected));
    wire usable;
    pa_and2 ug(.a(valid),.b(canonical),.y(usable));
    pa_and2 up(.a(usable),.b(selected),.y(predicate));
    pa_not ur(.a(usable),.y(rejected));
endmodule

// BIN+VSC operation: 00 query, 01 multiply by selected S4 prime,
// 10 exact-cancel selected prime, 11 invalidate exact sidecar after addition or
// another unmodelled structure-transforming operation. Rejected updates are
// atomic. Zero is exact: query is true and scale/cancel preserve zero and its
// all-true finite threshold sidecar.
module pa_bin_vsc_s4 #(
    parameter integer W=4,
    parameter integer T2=3, parameter integer T3=2,
    parameter integer T5=1, parameter integer T7=1
)(
    input wire [W-1:0] magnitude,
    input wire sidecar_valid,
    input wire [T2-1:0] t2,input wire [T3-1:0] t3,
    input wire [T5-1:0] t5,input wire [T7-1:0] t7,
    input wire [1:0] operation,
    input wire [1:0] prime_select,
    output wire [W-1:0] magnitude_y,
    output wire valid_y,
    output wire [T2-1:0] y2,output wire [T3-1:0] y3,
    output wire [T5-1:0] y5,output wire [T7-1:0] y7,
    output wire predicate,
    output wire rejected,
    output wire overflow
);
    wire [3:0] select;
    wire canonical,input_good,input_bad;
    wire magnitude_zero,magnitude_nonzero;
    wire selected_present;
    wire query_predicate,query_reject;
    pa_decode2 decoder(.select(prime_select),.one_hot(select));
    pa_equal_constant #(.W(W),.VALUE(0)) zero_check(.a(magnitude),.equal(magnitude_zero));
    pa_not nonzero_check(.a(magnitude_zero),.y(magnitude_nonzero));
    pa_therm_validate_s4 #(.T2(T2),.T3(T3),.T5(T5),.T7(T7)) u_checker(.t2(t2),.t3(t3),.t5(t5),.t7(t7),.valid(canonical));
    pa_and2 good(.a(sidecar_valid),.b(canonical),.y(input_good));
    pa_not bad(.a(input_good),.y(input_bad));
    pa_vsc_query_s4 #(.T2(T2),.T3(T3),.T5(T5),.T7(T7)) query(.valid(sidecar_valid),.t2(t2),.t3(t3),.t5(t5),.t7(t7),.prime_select(prime_select),.predicate(query_predicate),.rejected(query_reject));
    assign selected_present=query_predicate;

    wire [W-1:0] f23,f57,factor_word;
    localparam [W-1:0] P2=2,P3=3,P5=5,P7=7;
    pa_mux_word #(.W(W)) fm0(.d0(P2),.d1(P3),.sel(prime_select[0]),.y(f23));
    pa_mux_word #(.W(W)) fm1(.d0(P5),.d1(P7),.sel(prime_select[0]),.y(f57));
    pa_mux_word #(.W(W)) fmp(.d0(f23),.d1(f57),.sel(prime_select[1]),.y(factor_word));
    wire [2*W-1:0] product;
    wire product_high;
    pa_shift_add_multiplier #(.W(W)) mul(.a(magnitude),.b(factor_word),.product(product));
    pa_reduce_or #(.W(W)) high(.a(product[2*W-1:W]),.y(product_high));

    wire [T2-1:0] inc2,dec2, mul2,can2;
    wire [T3-1:0] inc3,dec3, mul3,can3;
    wire [T5-1:0] inc5,dec5, mul5,can5;
    wire [T7-1:0] inc7,dec7, mul7,can7;
    wire [3:0] lane_saturated;
    pa_therm_increment_lane #(.T(T2)) i2(.a(t2),.y(inc2),.saturated(lane_saturated[0]));
    pa_therm_increment_lane #(.T(T3)) i3(.a(t3),.y(inc3),.saturated(lane_saturated[1]));
    pa_therm_increment_lane #(.T(T5)) i5(.a(t5),.y(inc5),.saturated(lane_saturated[2]));
    pa_therm_increment_lane #(.T(T7)) i7(.a(t7),.y(inc7),.saturated(lane_saturated[3]));
    pa_therm_decrement_lane #(.T(T2)) d2(.a(t2),.y(dec2));
    pa_therm_decrement_lane #(.T(T3)) d3(.a(t3),.y(dec3));
    pa_therm_decrement_lane #(.T(T5)) d5(.a(t5),.y(dec5));
    pa_therm_decrement_lane #(.T(T7)) d7(.a(t7),.y(dec7));
    pa_mux_word #(.W(T2)) um2(.d0(t2),.d1(inc2),.sel(select[0]),.y(mul2));
    pa_mux_word #(.W(T3)) um3(.d0(t3),.d1(inc3),.sel(select[1]),.y(mul3));
    pa_mux_word #(.W(T5)) um5(.d0(t5),.d1(inc5),.sel(select[2]),.y(mul5));
    pa_mux_word #(.W(T7)) um7(.d0(t7),.d1(inc7),.sel(select[3]),.y(mul7));
    pa_mux_word #(.W(T2)) uc2(.d0(t2),.d1(dec2),.sel(select[0]),.y(can2));
    pa_mux_word #(.W(T3)) uc3(.d0(t3),.d1(dec3),.sel(select[1]),.y(can3));
    pa_mux_word #(.W(T5)) uc5(.d0(t5),.d1(dec5),.sel(select[2]),.y(can5));
    pa_mux_word #(.W(T7)) uc7(.d0(t7),.d1(dec7),.sel(select[3]),.y(can7));
    wire sat01,sat23,selected_sat;
    pa_mux2 sm0(.d0(lane_saturated[0]),.d1(lane_saturated[1]),.sel(prime_select[0]),.y(sat01));
    pa_mux2 sm1(.d0(lane_saturated[2]),.d1(lane_saturated[3]),.sel(prime_select[0]),.y(sat23));
    pa_mux2 smp(.d0(sat01),.d1(sat23),.sel(prime_select[1]),.y(selected_sat));

    wire [W-1:0] q2,q3,q5,q7,q23,q57,quotient;
    wire [3:0] qexact;
    pa_divide_constant_exact #(.W(W),.DIVISOR(2)) qd2(.magnitude(magnitude),.quotient(q2),.exact(qexact[0]));
    pa_divide_constant_exact #(.W(W),.DIVISOR(3)) qd3(.magnitude(magnitude),.quotient(q3),.exact(qexact[1]));
    pa_divide_constant_exact #(.W(W),.DIVISOR(5)) qd5(.magnitude(magnitude),.quotient(q5),.exact(qexact[2]));
    pa_divide_constant_exact #(.W(W),.DIVISOR(7)) qd7(.magnitude(magnitude),.quotient(q7),.exact(qexact[3]));
    pa_mux_word #(.W(W)) qm0(.d0(q2),.d1(q3),.sel(prime_select[0]),.y(q23));
    pa_mux_word #(.W(W)) qm1(.d0(q5),.d1(q7),.sel(prime_select[0]),.y(q57));
    pa_mux_word #(.W(W)) qmp(.d0(q23),.d1(q57),.sel(prime_select[1]),.y(quotient));
    wire qe01,qe23,selected_qexact;
    pa_mux2 qe0(.d0(qexact[0]),.d1(qexact[1]),.sel(prime_select[0]),.y(qe01));
    pa_mux2 qe1(.d0(qexact[2]),.d1(qexact[3]),.sel(prime_select[0]),.y(qe23));
    pa_mux2 qep(.d0(qe01),.d1(qe23),.sel(prime_select[1]),.y(selected_qexact));

    wire selected_sat_nonzero;
    wire mul_bad0,mul_reject,nonzero_cancel_ready,cancel_ready,cancel_reject;
    pa_and2 ms0(.a(selected_sat),.b(magnitude_nonzero),.y(selected_sat_nonzero));
    pa_or2 mb0(.a(input_bad),.b(product_high),.y(mul_bad0));
    pa_or2 mb1(.a(mul_bad0),.b(selected_sat_nonzero),.y(mul_reject));
    pa_and2 cr0(.a(selected_present),.b(selected_qexact),.y(nonzero_cancel_ready));
    pa_or2 crz(.a(magnitude_zero),.b(nonzero_cancel_ready),.y(cancel_ready));
    wire cancel_good;
    pa_and2 cr1(.a(input_good),.b(cancel_ready),.y(cancel_good));
    pa_not cr2(.a(cancel_good),.y(cancel_reject));

    wire cancel_hold;
    wire [W-1:0] mul_mag,can_mag,low_mag,high_mag;
    wire [T2-1:0] mul_final2,can_final2,low2,high2;
    wire [T3-1:0] mul_final3,can_final3,low3,high3;
    wire [T5-1:0] mul_final5,can_final5,low5,high5;
    wire [T7-1:0] mul_final7,can_final7,low7,high7;
    // Accepted cancellation of zero holds the infinite-threshold encoding;
    // rejected cancellation also holds the complete input atomically.
    pa_or2 ch(.a(cancel_reject),.b(magnitude_zero),.y(cancel_hold));
    pa_mux_word #(.W(W)) ma(.d0(product[W-1:0]),.d1(magnitude),.sel(mul_reject),.y(mul_mag));
    pa_mux_word #(.W(W)) ca(.d0(quotient),.d1(magnitude),.sel(cancel_hold),.y(can_mag));
    pa_mux_word #(.W(T2)) ma2(.d0(mul2),.d1(t2),.sel(mul_reject),.y(mul_final2));
    pa_mux_word #(.W(T3)) ma3(.d0(mul3),.d1(t3),.sel(mul_reject),.y(mul_final3));
    pa_mux_word #(.W(T5)) ma5(.d0(mul5),.d1(t5),.sel(mul_reject),.y(mul_final5));
    pa_mux_word #(.W(T7)) ma7(.d0(mul7),.d1(t7),.sel(mul_reject),.y(mul_final7));
    pa_mux_word #(.W(T2)) ca2(.d0(can2),.d1(t2),.sel(cancel_hold),.y(can_final2));
    pa_mux_word #(.W(T3)) ca3(.d0(can3),.d1(t3),.sel(cancel_hold),.y(can_final3));
    pa_mux_word #(.W(T5)) ca5(.d0(can5),.d1(t5),.sel(cancel_hold),.y(can_final5));
    pa_mux_word #(.W(T7)) ca7(.d0(can7),.d1(t7),.sel(cancel_hold),.y(can_final7));
    pa_mux_word #(.W(W)) om0(.d0(magnitude),.d1(mul_mag),.sel(operation[0]),.y(low_mag));
    pa_mux_word #(.W(W)) om1(.d0(can_mag),.d1(magnitude),.sel(operation[0]),.y(high_mag));
    pa_mux_word #(.W(W)) omp(.d0(low_mag),.d1(high_mag),.sel(operation[1]),.y(magnitude_y));
    pa_mux_word #(.W(T2)) o20(.d0(t2),.d1(mul_final2),.sel(operation[0]),.y(low2));
    pa_mux_word #(.W(T2)) o21(.d0(can_final2),.d1(t2),.sel(operation[0]),.y(high2));
    pa_mux_word #(.W(T2)) o2p(.d0(low2),.d1(high2),.sel(operation[1]),.y(y2));
    pa_mux_word #(.W(T3)) o30(.d0(t3),.d1(mul_final3),.sel(operation[0]),.y(low3));
    pa_mux_word #(.W(T3)) o31(.d0(can_final3),.d1(t3),.sel(operation[0]),.y(high3));
    pa_mux_word #(.W(T3)) o3p(.d0(low3),.d1(high3),.sel(operation[1]),.y(y3));
    pa_mux_word #(.W(T5)) o50(.d0(t5),.d1(mul_final5),.sel(operation[0]),.y(low5));
    pa_mux_word #(.W(T5)) o51(.d0(can_final5),.d1(t5),.sel(operation[0]),.y(high5));
    pa_mux_word #(.W(T5)) o5p(.d0(low5),.d1(high5),.sel(operation[1]),.y(y5));
    pa_mux_word #(.W(T7)) o70(.d0(t7),.d1(mul_final7),.sel(operation[0]),.y(low7));
    pa_mux_word #(.W(T7)) o71(.d0(can_final7),.d1(t7),.sel(operation[0]),.y(high7));
    pa_mux_word #(.W(T7)) o7p(.d0(low7),.d1(high7),.sel(operation[1]),.y(y7));

    wire low_valid,high_valid;
    pa_mux2 ov0(.d0(sidecar_valid),.d1(sidecar_valid),.sel(operation[0]),.y(low_valid));
    pa_mux2 ov1(.d0(sidecar_valid),.d1(1'b0),.sel(operation[0]),.y(high_valid));
    pa_mux2 ovp(.d0(low_valid),.d1(high_valid),.sel(operation[1]),.y(valid_y));
    wire low_reject,high_reject;
    pa_mux2 or0(.d0(query_reject),.d1(mul_reject),.sel(operation[0]),.y(low_reject));
    pa_mux2 or1(.d0(cancel_reject),.d1(1'b0),.sel(operation[0]),.y(high_reject));
    pa_mux2 orp(.d0(low_reject),.d1(high_reject),.sel(operation[1]),.y(rejected));
    wire op0_not,op1_not,mul_opcode;
    pa_not on0(.a(operation[0]),.y(op0_not));
    pa_not on1(.a(operation[1]),.y(op1_not));
    pa_and2 mop(.a(operation[0]),.b(op1_not),.y(mul_opcode));
    pa_and2 ovf(.a(product_high),.b(mul_opcode),.y(overflow));
    wire query_opcode;
    pa_and2 qop(.a(op0_not),.b(op1_not),.y(query_opcode));
    pa_and2 pred(.a(query_predicate),.b(query_opcode),.y(predicate));
endmodule

`default_nettype wire
